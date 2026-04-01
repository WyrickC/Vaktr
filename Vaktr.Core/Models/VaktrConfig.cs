using System.Text.Json.Serialization;

namespace Vaktr.Core.Models;

public sealed class VaktrConfig
{
    private const int DefaultScrapeIntervalSecondsValue = 2;
    private const int DefaultGraphWindowMinutesValue = 15;
    private const int DefaultMaxRetentionHoursValue = 24;

    public int ScrapeIntervalSeconds { get; set; } = DefaultScrapeIntervalSecondsValue;

    public int GraphWindowMinutes { get; set; } = DefaultGraphWindowMinutesValue;

    public int MaxRetentionHours { get; set; }

    public RetentionPreset Retention { get; set; } = RetentionPreset.OneDay;

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public string StorageDirectory { get; set; } = DefaultStorageDirectory;

    public bool LaunchOnStartup { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public Dictionary<string, bool> PanelVisibility { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public static int DefaultScrapeIntervalSeconds => DefaultScrapeIntervalSecondsValue;

    [JsonIgnore]
    public static int DefaultGraphWindowMinutes => DefaultGraphWindowMinutesValue;

    [JsonIgnore]
    public static int DefaultMaxRetentionHours => DefaultMaxRetentionHoursValue;

    [JsonIgnore]
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vaktr");

    [JsonIgnore]
    public static string LegacySettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vaktr");

    [JsonIgnore]
    public static string DefaultStorageDirectory =>
        Path.Combine(SettingsDirectory, "Data");

    public string GetDatabasePath() => Path.Combine(StorageDirectory, "vaktr-metrics.db");

    public static string GetConfigPath() => Path.Combine(SettingsDirectory, "vaktr-settings.json");

    public static string GetLegacyConfigPath() => Path.Combine(LegacySettingsDirectory, "vaktr-settings.json");

    public VaktrConfig Normalize()
    {
        if (ScrapeIntervalSeconds is < 1 or > 60)
        {
            ScrapeIntervalSeconds = DefaultScrapeIntervalSecondsValue;
        }

        if (GraphWindowMinutes is < 1 or > 60)
        {
            GraphWindowMinutes = DefaultGraphWindowMinutesValue;
        }

        if (MaxRetentionHours is < 1 or > 24 * 3650)
        {
            MaxRetentionHours = Retention switch
            {
                RetentionPreset.OneDay => 24,
                RetentionPreset.SevenDays => 24 * 7,
                RetentionPreset.ThirtyDays => 24 * 30,
                RetentionPreset.NinetyDays => 24 * 90,
                RetentionPreset.Unlimited => DefaultMaxRetentionHoursValue,
                _ => DefaultMaxRetentionHoursValue,
            };
        }

        Retention = MaxRetentionHours switch
        {
            <= 24 => RetentionPreset.OneDay,
            <= 24 * 7 => RetentionPreset.SevenDays,
            <= 24 * 30 => RetentionPreset.ThirtyDays,
            <= 24 * 90 => RetentionPreset.NinetyDays,
            _ => RetentionPreset.NinetyDays,
        };

        StorageDirectory = string.IsNullOrWhiteSpace(StorageDirectory)
            ? DefaultStorageDirectory
            : StorageDirectory.Trim();

        PanelVisibility ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        return this;
    }

    public static VaktrConfig CreateDefault() => new()
    {
        StorageDirectory = DefaultStorageDirectory,
        MaxRetentionHours = DefaultMaxRetentionHoursValue,
    };
}
