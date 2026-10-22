using System.Threading;
using System.Windows;
using AK980Studio.Services;

namespace AK980Studio;

public partial class App : Application
{
    private const string InstanceName = "AK980Studio.SingleInstance";
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private TrayIcon? _tray;

    public static AppSettings Settings { get; private set; } = new();
    public static KeyboardService Keyboard { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, InstanceName, out bool first);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!first)
        {
            // Already running: ask that instance to show its window, then quit.
            _showSignal.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        Settings = AppSettings.Load();
        ThemeManager.Apply(Settings);
        Keyboard = new KeyboardService(Settings);

        var window = new MainWindow();
        MainWindow = window;
        _tray = new TrayIcon(ShowMain, ExitApp);

        bool minimized = e.Args.Contains("--minimized") && Settings.StartMinimized;
        if (!minimized) window.Show();

        new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.Invoke(ShowMain);
        }) { IsBackground = true }.Start();

        Keyboard.BatteryChanged += () => Dispatcher.Invoke(OnBatteryChanged);
        OnBatteryChanged();
        Keyboard.Start();
    }

    private bool _lowBatteryWarned;

    private void OnBatteryChanged()
    {
        _tray?.SetText(BatteryFormat.TrayText(Keyboard));
        if (!Keyboard.IsWireless || Keyboard.BatteryPercent is not int pct) return;
        int threshold = Settings.BatteryWarnPercent;
        if (Settings.BatteryWarn && pct < threshold && !_lowBatteryWarned)
        {
            _lowBatteryWarned = true;
            _tray?.Notify("AK980 battery low", $"{pct}% left. Plug in the USB cable to charge.", warning: true);
        }
        else if (pct >= threshold + 5)
        {
            _lowBatteryWarned = false; // re-arm after charging
        }
    }

    public void ShowMain()
    {
        var w = MainWindow;
        if (w is null) return;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
    }

    public void ExitApp()
    {
        Settings.Save();
        Keyboard.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
