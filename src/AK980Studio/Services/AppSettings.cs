using System.IO;
using System.Text.Json;
using Ajazz.Keyboard;
using Ajazz.Keyboard.Lcd;

namespace AK980Studio.Services;

public sealed record LightPreset(string Name, LightSettings Light);

/// <summary>Everything the user can customize, persisted to %LOCALAPPDATA%\AK980Studio\settings.json.</summary>
public sealed class AppSettings
{
    // Look & feel
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#B48CFF";
    public double UiScale { get; set; } = 1.0;

    // Behaviour
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool RestoreLightingOnConnect { get; set; }

    /// <summary>The stock app always did this on launch, which is what reset the screen. Off by default.</summary>
    public bool SyncClockOnConnect { get; set; }
    public bool SyncClockHourly { get; set; }

    /// <summary>LCD slot the keyboard should show. Uploads and clock syncs both use it, so they never disagree.</summary>
    public byte LcdSlot { get; set; } = 1;
    public int CommandDelayMs { get; set; } = 35;

    // LCD editor defaults
    public LcdFitMode LcdFit { get; set; } = LcdFitMode.Fit;
    public string LcdBackground { get; set; } = "#000000";
    public bool LcdDither { get; set; } = true;
    public double LcdSpeed { get; set; } = 1.0;
    public string? LastUploadedId { get; set; }

    // Lighting
    public LightSettings Light { get; set; } = new();
    public bool LiveApply { get; set; } = true;
    public List<LightPreset> LightPresets { get; set; } = new();

    // Per-key custom colors: LED index -> "#RRGGBB"
    public Dictionary<int, string> PerKeyColors { get; set; } = new();
    public int PerKeyBrightness { get; set; } = LightSettings.MaxLevel;

    // Firmware options (Keyboard page)
    public KeyboardOptions KeyboardOptions { get; set; } = new();

    // Battery (read through the 2.4G dongle; the keyboard only answers in 2.4G mode)
    public int BatteryPollSeconds { get; set; } = 15;
    public bool BatteryWarn { get; set; } = true;
    public int BatteryWarnPercent { get; set; } = 20;
    public int? LastBatteryPercent { get; set; }
    public DateTime? LastBatteryTime { get; set; }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AK980Studio");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (Exception)
        {
            // A corrupt settings file should never stop the app from opening.
        }
        // First run: start from whatever the stock app last put on the keyboard.
        var fresh = new AppSettings();
        if (StockImporter.ReadActiveLighting() is { } light) fresh.Light = light;
        return fresh;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
