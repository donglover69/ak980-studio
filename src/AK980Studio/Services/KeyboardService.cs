using System.Windows.Threading;
using Ajazz.Keyboard;
using Ajazz.Keyboard.Lcd;

namespace AK980Studio.Services;

/// <summary>
/// Owns the keyboard connection: polls for plug/unplug, and runs every device operation on one
/// background worker so the UI never blocks and commands never interleave.
/// Nothing is sent on connect unless the user enabled it in settings.
/// </summary>
public sealed class KeyboardService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _poll;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Ak980Keyboard? _kb;
    private DateTime _lastClockSync = DateTime.MinValue;

    public event Action? StatusChanged;

    public bool IsConnected => _kb is not null;
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Looking for keyboard...";

    public KeyboardService(AppSettings settings)
    {
        _settings = settings;
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += async (_, _) => await PollAsync();
    }

    public void Start()
    {
        _poll.Start();
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (IsBusy) return;
        bool present = await Task.Run(Ak980Keyboard.IsPresent);
        if (present && _kb is null)
        {
            try
            {
                _kb = await Task.Run(Ak980Keyboard.Open);
            }
            catch (Exception ex)
            {
                SetStatus($"Keyboard found but could not be opened: {ex.Message}");
                return;
            }
            if (_kb is null)
            {
                SetStatus("2.4G dongle detected. Plug in the USB cable to configure the keyboard.");
                return;
            }
            _kb.CommandDelayMs = _settings.CommandDelayMs;
            SetStatus($"Connected: {_kb.Product} (USB)");
            await OnConnectedAsync();
        }
        else if (!present && _kb is not null)
        {
            _kb.Dispose();
            _kb = null;
            SetStatus("Keyboard disconnected");
        }
        else if (_kb is not null && _settings.SyncClockHourly && DateTime.Now - _lastClockSync > TimeSpan.FromHours(1))
        {
            await RunAsync(kb => SyncClock(kb), "Clock synced");
        }
    }

    private async Task OnConnectedAsync()
    {
        if (_settings.RestoreLightingOnConnect)
            await RunAsync(kb => kb.SetLighting(_settings.Light), null);
        if (_settings.SyncClockOnConnect)
            await RunAsync(kb => SyncClock(kb), null);
    }

    private void SyncClock(Ak980Keyboard kb)
    {
        kb.SyncClock(DateTime.Now, _settings.LcdSlot);
        _lastClockSync = DateTime.Now;
    }

    /// <summary>Runs <paramref name="op"/> on the worker. Returns false (and reports why) if it failed.</summary>
    public async Task<bool> RunAsync(Action<Ak980Keyboard> op, string? doneMessage)
    {
        var kb = _kb;
        if (kb is null)
        {
            SetStatus("Keyboard not connected (USB cable required)");
            return false;
        }
        await _gate.WaitAsync();
        IsBusy = true;
        StatusChanged?.Invoke();
        try
        {
            kb.CommandDelayMs = _settings.CommandDelayMs;
            await Task.Run(() => op(kb));
            if (doneMessage is not null) SetStatus(doneMessage);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            // A failed transfer usually means the device went away; force a reconnect on the next poll.
            _kb?.Dispose();
            _kb = null;
            return false;
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
            StatusChanged?.Invoke();
        }
    }

    public Task<bool> SyncClockAsync() => RunAsync(SyncClock, $"Clock synced ({DateTime.Now:HH:mm})");

    public Task<bool> SetLightingAsync(LightSettings s) => RunAsync(kb => kb.SetLighting(s), null);

    public Task<bool> SetCustomColorsAsync(IReadOnlyDictionary<int, (byte, byte, byte)> colors, int brightness) =>
        RunAsync(kb => kb.SetCustomColors(colors, brightness), "Per-key colors applied");

    public Task<bool> SetOptionsAsync(KeyboardOptions o) => RunAsync(kb => kb.SetOptions(o), "Keyboard settings saved");

    public Task<bool> UploadAsync(LcdAnimation anim, IProgress<double> progress) =>
        RunAsync(kb => kb.UploadLcd(anim, _settings.LcdSlot, progress), "Upload complete");

    private void SetStatus(string s)
    {
        Status = s;
        StatusChanged?.Invoke();
    }

    public void Dispose()
    {
        _poll.Stop();
        _kb?.Dispose();
    }
}
