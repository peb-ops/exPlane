using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExPlane.Services;

public sealed class AppSettings
{
    public bool Topmost { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+T";
    public WindowSettings Window { get; set; } = new();
    public List<ClockSettings> Clocks { get; set; } = new();
    /// <summary>null = follow the Windows time format until the user picks one.</summary>
    public bool? Use24HourClock { get; set; }
    public LauncherSettings Launcher { get; set; } = new();
}

public sealed class LauncherSettings
{
    public List<LauncherGroupSettings> Groups { get; set; } = new();
}

public sealed class LauncherGroupSettings
{
    public string Name { get; set; } = "";
    public bool Collapsed { get; set; }
    /// <summary>Set for folder-portal groups; their items come from the folder and aren't stored.</summary>
    public string? PortalPath { get; set; }
    public List<LauncherItemSettings> Items { get; set; } = new();
}

public sealed class LauncherItemSettings
{
    public string Label { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RunAsAdmin { get; set; }
}

public sealed class WindowSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public sealed class ClockSettings
{
    public string TimeZoneId { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>Reads and writes %APPDATA%\exPlane\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "exPlane", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"exPlane: failed to load settings, using defaults: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Write-then-replace so a crash mid-write can't leave a truncated file.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"exPlane: failed to save settings: {ex.Message}");
        }
    }
}
