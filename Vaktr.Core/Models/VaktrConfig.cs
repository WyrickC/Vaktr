using System.Text.Json.Serialization;

namespace Vaktr.Core.Models;

public sealed class VaktrConfig
{
    private const int DefaultScrapeIntervalSeconds = 2;
    private const int DefaultGraphWindowMinutes = 15;

    public int ScrapeIntervalSeconds { get; set; } = DefaultScrapeIntervalSeconds;

    public int GraphWindowMinutes { get; set; } = DefaultGraphWindowMinutes;

    public RetentionPreset Retention { get; set; } = RetentionPreset.ThirtyDays;

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public string StorageDirectory { get; set; } = DefaultStorageDirectory;

    public bool LaunchOnStartup { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public Dictionary<string, bool> PanelVisibility { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vaktr");

    [JsonIgnore]
    public static string DefaultStorageDirectory =>
        Path.Combine(SettingsDirectory, "Data");

    public string GetDatabasePath() => Path.Combine(StorageDirectory, "vaktr-metrics.db");

    public static string GetConfigPath() => Path.Combine(SettingsDirectory, "vaktr-settings.json");

    public VaktrConfig Normalize()
    {
        if (ScrapeIntervalSeconds is < 1 or > 60)
        {
            ScrapeIntervalSeconds = DefaultScrapeIntervalSeconds;
        }

        if (GraphWindowMinutes is < 1 or > 60)
        {
            GraphWindowMinutes = DefaultGraphWindowMinutes;
        }

        StorageDirectory = string.IsNullOrWhiteSpace(StorageDirectory)
            ? DefaultStorageDirectory
            : StorageDirectory;

        PanelVisibility ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        return this;
    }

    public static VaktrConfig CreateDefault() => new()
    {
        StorageDirectory = DefaultStorageDirectory,
    };
}
