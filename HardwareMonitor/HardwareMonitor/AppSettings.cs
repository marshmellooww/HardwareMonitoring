using System.IO;
using System.Text.Json;

namespace HardwareMonitor;

/// <summary>
/// User preferences for the overlay, persisted as JSON under %AppData%.
/// </summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HardwareMonitor",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool IsHorizontal { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public bool IsPositionLocked { get; set; }

    /// <summary>Stats turned off in the Sensors menu, by row tag (e.g. "RAM").</summary>
    public List<string> HiddenStats { get; set; } = new();

    /// <summary>Last window position; null means "use the default top-right corner".</summary>
    public double? Left { get; set; }
    public double? Top { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable or corrupt settings; fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth interrupting the overlay over; settings just won't persist.
        }
    }
}
