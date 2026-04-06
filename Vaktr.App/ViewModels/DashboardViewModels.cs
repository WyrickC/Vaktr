using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Vaktr.Core.Models;

namespace Vaktr.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, MetricPanelViewModel> _panelLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _panelOrder = new(StringComparer.OrdinalIgnoreCase);
    private bool _dashboardPanelsDirty = true;
    private bool _panelTogglesDirty = true;

    private string _storageDirectory = string.Empty;
    private string _scrapeIntervalInput = string.Empty;
    private string _retentionHoursInput = string.Empty;
    private int _selectedIntervalSeconds = VaktrConfig.DefaultScrapeIntervalSeconds;
    private int _selectedWindowMinutes = 15;
    private ThemeMode _selectedTheme = ThemeMode.Dark;
    private bool _launchOnStartup;
    private bool _minimizeToTray = true;
    private bool _collectWhenMinimized;
    private bool _isSettingsOpen;
    private string _statusText = "Arming telemetry deck";
    private MetricPanelViewModel? _expandedPanel;
    private TimeSpan _retentionWindow = TimeSpan.FromHours(VaktrConfig.DefaultMaxRetentionHours);

    public MainViewModel(VaktrConfig config)
    {
        IntervalOptions =
        [
            new SelectionOption(1, "1 second"),
            new SelectionOption(2, "2 seconds"),
            new SelectionOption(5, "5 seconds"),
            new SelectionOption(10, "10 seconds"),
            new SelectionOption(30, "30 seconds"),
        ];

        WindowOptions =
        [
            new SelectionOption(5, "5 minutes"),
            new SelectionOption(30, "30 minutes"),
            new SelectionOption(60, "1 hour"),
            new SelectionOption(720, "12 hours"),
            new SelectionOption(1440, "24 hours"),
            new SelectionOption(10080, "7 days"),
            new SelectionOption(43200, "30 days"),
        ];

        RetentionOptions =
        [
            new SelectionOption(1, "1 day"),
            new SelectionOption(7, "7 days"),
            new SelectionOption(30, "30 days"),
            new SelectionOption(90, "90 days"),
            new SelectionOption(0, "Forever"),
        ];

        ThemeOptions =
        [
            new ThemeSelectionOption(ThemeMode.Dark, "Dark"),
            new ThemeSelectionOption(ThemeMode.Light, "Light"),
        ];

        SummaryCards =
        [
            new SummaryCardViewModel("CPU", "CPU", BrushFactory.CreateBrush("#5DE6FF")),
            new SummaryCardViewModel("RAM", "Memory", BrushFactory.CreateBrush("#7BF7D0")),
            new SummaryCardViewModel("IO", "Disk", BrushFactory.CreateBrush("#FF9B54")),
            new SummaryCardViewModel("WAN", "Network", BrushFactory.CreateBrush("#9A8CFF")),
        ];

        EnsureBaselinePanels();
        ApplyConfig(config);
    }

    public ObservableCollection<MetricPanelViewModel> DashboardPanels { get; } = [];

    public ObservableCollection<PanelToggleViewModel> PanelToggles { get; } = [];

    public IReadOnlyList<SummaryCardViewModel> SummaryCards { get; }

    public IReadOnlyList<SelectionOption> IntervalOptions { get; }

    public IReadOnlyList<SelectionOption> WindowOptions { get; }

    public IReadOnlyList<SelectionOption> RetentionOptions { get; }

    public IReadOnlyList<ThemeSelectionOption> ThemeOptions { get; }

    public string StorageDirectory
    {
        get => _storageDirectory;
        set => SetProperty(ref _storageDirectory, value);
    }

    public string ScrapeIntervalInput
    {
        get => _scrapeIntervalInput;
        set => SetProperty(ref _scrapeIntervalInput, value);
    }

    public int SelectedIntervalSeconds
    {
        get => _selectedIntervalSeconds;
        set => SetProperty(ref _selectedIntervalSeconds, value);
    }

    public int SelectedWindowMinutes
    {
        get => _selectedWindowMinutes;
        set
        {
            if (!SetProperty(ref _selectedWindowMinutes, value))
            {
                return;
            }

            foreach (var panel in _panelLookup.Values)
            {
                panel.SelectedRange = MapToRangePreset(value);
            }
        }
    }

    public void ApplyGlobalWindowRange(int minutes)
    {
        if (_selectedWindowMinutes != minutes)
        {
            _selectedWindowMinutes = minutes;
            RaisePropertyChanged(nameof(SelectedWindowMinutes));
        }

        var preset = MapToRangePreset(minutes);
        foreach (var panel in _panelLookup.Values)
        {
            panel.ApplyRangePreset(preset);
        }
    }

    public void ApplyGlobalAbsoluteRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        foreach (var panel in _panelLookup.Values)
        {
            panel.ZoomToWindow(startUtc, endUtc);
        }
    }

    public void ResetGlobalZoom()
    {
        foreach (var panel in _panelLookup.Values)
        {
            panel.ResetZoom();
        }
    }

    public string RetentionHoursInput
    {
        get => _retentionHoursInput;
        set => SetProperty(ref _retentionHoursInput, value);
    }

    public ThemeMode SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set => SetProperty(ref _launchOnStartup, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool CollectWhenMinimized
    {
        get => _collectWhenMinimized;
        set => SetProperty(ref _collectWhenMinimized, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public MetricPanelViewModel? ExpandedPanel
    {
        get => _expandedPanel;
        set => SetProperty(ref _expandedPanel, value);
    }

    public VaktrConfig BuildConfig()
    {
        var visibility = PanelToggles.ToDictionary(toggle => toggle.PanelKey, toggle => toggle.IsVisible, StringComparer.OrdinalIgnoreCase);

        return new VaktrConfig
        {
            ScrapeIntervalSeconds = EffectiveScrapeIntervalSeconds,
            GraphWindowMinutes = SelectedWindowMinutes,
            MaxRetentionHours = EffectiveRetentionHours,
            RetentionInputText = NormalizeRetentionInputForPersistence(RetentionHoursInput),
            Theme = SelectedTheme,
            StorageDirectory = EffectiveStorageDirectory,
            LaunchOnStartup = LaunchOnStartup,
            MinimizeToTray = MinimizeToTray,
            CollectWhenMinimized = CollectWhenMinimized,
            PanelVisibility = visibility,
            PanelOrder = BuildPersistedPanelOrder().ToList(),
        }.Normalize();
    }

    public void ApplyConfig(VaktrConfig config)
    {
        var normalized = config.Normalize();
        EnsureBaselinePanels();
        _retentionWindow = normalized.GetRetentionWindow();
        StorageDirectory = string.Equals(normalized.StorageDirectory, VaktrConfig.DefaultStorageDirectory, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized.StorageDirectory;
        ScrapeIntervalInput = normalized.ScrapeIntervalSeconds == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : normalized.ScrapeIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RetentionHoursInput = !string.IsNullOrWhiteSpace(normalized.RetentionInputText)
            ? normalized.RetentionInputText
            : normalized.MaxRetentionHours == VaktrConfig.DefaultMaxRetentionHours
                ? string.Empty
                : FormatRetentionInput(normalized.MaxRetentionHours);
        SelectedIntervalSeconds = normalized.ScrapeIntervalSeconds;
        SelectedWindowMinutes = normalized.GraphWindowMinutes;
        SelectedTheme = normalized.Theme;
        LaunchOnStartup = normalized.LaunchOnStartup;
        MinimizeToTray = normalized.MinimizeToTray;
        CollectWhenMinimized = normalized.CollectWhenMinimized;
        ApplyPanelOrder(normalized.PanelOrder);

        foreach (var toggle in PanelToggles)
        {
            toggle.IsVisible = normalized.PanelVisibility.GetValueOrDefault(toggle.PanelKey, true);
        }

        foreach (var panel in _panelLookup.Values)
        {
            panel.SetRetentionWindow(_retentionWindow);
            panel.IsVisible = normalized.PanelVisibility.GetValueOrDefault(panel.PanelKey, panel.IsDashboardPanel);
            panel.SelectedRange = MapToRangePreset(normalized.GraphWindowMinutes);
        }

        _dashboardPanelsDirty = true;
        _panelTogglesDirty = true;
        SyncDashboardPanels();
        SyncPanelToggles();
    }

    public void LoadHistory(IReadOnlyList<MetricSeriesHistory> history)
    {
        var panelCreated = false;
        foreach (var panelHistory in history)
        {
            var panel = GetOrCreatePanel(panelHistory.PanelKey, panelHistory.PanelTitle, panelHistory.Category, panelHistory.Unit);
            panel.LoadHistory(panelHistory);
            panelCreated = true;
        }

        if (panelCreated)
        {
            _dashboardPanelsDirty = true;
            _panelTogglesDirty = true;
            SyncDashboardPanels();
            SyncPanelToggles();
        }
    }

    public void ApplySnapshot(MetricSnapshot snapshot)
    {
        var panelCreated = false;
        foreach (var sample in snapshot.Samples)
        {
            var panel = GetOrCreatePanel(sample.PanelKey, sample.PanelTitle, sample.Category, sample.Unit);
            panel.AppendSample(sample);
            panelCreated |= panel.IsNewlyCreated;
            panel.IsNewlyCreated = false;
        }

        var detailContext = PanelDetailContext.FromSnapshot(snapshot);
        foreach (var panel in _panelLookup.Values)
        {
            panel.ApplyDetailContext(detailContext);
        }

        StatusText = $"Last sample {snapshot.Timestamp.LocalDateTime:t}";
        UpdateSummaryCards(snapshot, detailContext);

        if (panelCreated)
        {
            _dashboardPanelsDirty = true;
            _panelTogglesDirty = true;
            SyncPanelToggles();
            SyncDashboardPanels();
        }
    }

    public void ApplyPanelVisibility()
    {
        var visibilityMap = PanelToggles.ToDictionary(toggle => toggle.PanelKey, toggle => toggle.IsVisible, StringComparer.OrdinalIgnoreCase);
        foreach (var panel in _panelLookup.Values)
        {
            panel.IsVisible = visibilityMap.GetValueOrDefault(panel.PanelKey, panel.IsDashboardPanel);
        }

        _dashboardPanelsDirty = true;
        SyncDashboardPanels();
    }

    public bool MovePanel(string movingKey, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(movingKey) ||
            string.IsNullOrWhiteSpace(targetKey) ||
            string.Equals(movingKey, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_panelLookup.ContainsKey(movingKey) || !_panelLookup.ContainsKey(targetKey))
        {
            return false;
        }

        var orderedKeys = BuildPersistedPanelOrder().ToList();
        var movingIndex = orderedKeys.FindIndex(key => string.Equals(key, movingKey, StringComparison.OrdinalIgnoreCase));
        var targetIndex = orderedKeys.FindIndex(key => string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase));
        if (movingIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        orderedKeys.RemoveAt(movingIndex);
        if (movingIndex < targetIndex)
        {
            targetIndex--;
        }

        orderedKeys.Insert(targetIndex, movingKey);
        ApplyPanelOrder(orderedKeys);

        _dashboardPanelsDirty = true;
        _panelTogglesDirty = true;
        SyncDashboardPanels();
        SyncPanelToggles();
        return true;
    }

    public int EffectiveScrapeIntervalSeconds =>
        ParseOptionalInt(ScrapeIntervalInput, VaktrConfig.DefaultScrapeIntervalSeconds, 1, 60);

    public int EffectiveRetentionHours =>
        TryParseRetentionInput(RetentionHoursInput, out var hours, out _)
            ? hours
            : VaktrConfig.DefaultMaxRetentionHours;

    public string EffectiveStorageDirectory =>
        string.IsNullOrWhiteSpace(StorageDirectory)
            ? VaktrConfig.DefaultStorageDirectory
            : StorageDirectory.Trim();

    public bool HasValidRetentionInput =>
        TryParseRetentionInput(RetentionHoursInput, out _, out _);

    public static bool TryParseRetentionInput(string? text, out int hours, out string normalizedText)
    {
        hours = VaktrConfig.DefaultMaxRetentionHours;
        normalizedText = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!VaktrConfig.TryParseRetentionWindow(text, out var retentionWindow, out normalizedText))
        {
            return false;
        }

        var totalHours = retentionWindow.TotalHours;
        if (totalHours > 24 * 3650)
        {
            return false;
        }

        hours = Math.Clamp((int)Math.Ceiling(totalHours), 1, 24 * 3650);
        return true;
    }

    public static string FormatRetentionInput(int hours)
    {
        if (hours > 0 && hours % 24 == 0)
        {
            return $"{hours / 24}d";
        }

        return $"{hours}h";
    }

    private static string NormalizeRetentionInputForPersistence(string? input)
    {
        return TryParseRetentionInput(input, out _, out var normalizedText)
            ? normalizedText
            : string.Empty;
    }

    private MetricPanelViewModel GetOrCreatePanel(string panelKey, string title, MetricCategory category, MetricUnit unit)
    {
        if (_panelLookup.TryGetValue(panelKey, out var existing))
        {
            return existing;
        }

        if (panelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase))
        {
            unit = MetricUnit.Percent;
        }

        var panel = new MetricPanelViewModel(panelKey, title, category, unit)
        {
            SelectedRange = MapToRangePreset(SelectedWindowMinutes),
            IsVisible = true,
            IsNewlyCreated = true,
        };
        panel.SetRetentionWindow(_retentionWindow);

        _panelLookup.Add(panelKey, panel);
        _dashboardPanelsDirty = true;
        _panelTogglesDirty = true;
        return panel;
    }

    private void SyncDashboardPanels()
    {
        if (!_dashboardPanelsDirty)
        {
            return;
        }

        var orderedPanels = _panelLookup.Values
            .Where(panel => panel.IsDashboardPanel && panel.IsVisible)
            .OrderBy(GetPanelOrderRank)
            .ThenBy(panel => panel.SortBucket)
            .ThenBy(panel => panel.SortGroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(panel => panel.SortVariant)
            .ThenBy(panel => panel.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (DashboardPanels.Count == orderedPanels.Length && DashboardPanels.SequenceEqual(orderedPanels))
        {
            _dashboardPanelsDirty = false;
            return;
        }

        DashboardPanels.Clear();
        foreach (var panel in orderedPanels)
        {
            DashboardPanels.Add(panel);
        }

        _dashboardPanelsDirty = false;
    }

    private void SyncPanelToggles()
    {
        if (!_panelTogglesDirty)
        {
            return;
        }

        var visibilityLookup = PanelToggles.ToDictionary(toggle => toggle.PanelKey, toggle => toggle.IsVisible, StringComparer.OrdinalIgnoreCase);
        var panels = _panelLookup.Values
            .Where(panel => panel.IsDashboardPanel)
            .OrderBy(GetPanelOrderRank)
            .ThenBy(panel => panel.SortBucket)
            .ThenBy(panel => panel.SortGroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(panel => panel.SortVariant)
            .ThenBy(panel => panel.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (PanelToggles.Count == panels.Length &&
            PanelToggles.Select(toggle => toggle.PanelKey).SequenceEqual(panels.Select(panel => panel.PanelKey), StringComparer.OrdinalIgnoreCase))
        {
            foreach (var toggle in PanelToggles)
            {
                toggle.IsVisible = visibilityLookup.GetValueOrDefault(toggle.PanelKey, true);
            }

            _panelTogglesDirty = false;
            return;
        }

        PanelToggles.Clear();
        foreach (var panel in panels)
        {
            PanelToggles.Add(new PanelToggleViewModel(panel.PanelKey, panel.Title, visibilityLookup.GetValueOrDefault(panel.PanelKey, panel.IsVisible)));
        }

        _panelTogglesDirty = false;
    }

    private void ApplyPanelOrder(IEnumerable<string>? orderedKeys)
    {
        _panelOrder.Clear();
        if (orderedKeys is null)
        {
            return;
        }

        var index = 0;
        foreach (var key in orderedKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || _panelOrder.ContainsKey(key))
            {
                continue;
            }

            _panelOrder[key] = index++;
        }
    }

    private IReadOnlyList<string> BuildPersistedPanelOrder()
    {
        return _panelLookup.Values
            .Where(panel => panel.IsDashboardPanel)
            .OrderBy(GetPanelOrderRank)
            .ThenBy(panel => panel.SortBucket)
            .ThenBy(panel => panel.SortGroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(panel => panel.SortVariant)
            .ThenBy(panel => panel.Title, StringComparer.OrdinalIgnoreCase)
            .Select(panel => panel.PanelKey)
            .ToArray();
    }

    private int GetPanelOrderRank(MetricPanelViewModel panel)
    {
        return _panelOrder.TryGetValue(panel.PanelKey, out var rank)
            ? rank
            : int.MaxValue;
    }

    private void EnsureBaselinePanels()
    {
        // GPU temp shows up without a kernel driver; CPU temp requires PawnIO (roadmap item)
        EnsureBaselinePanel("gpu-temperature", "GPU Temperature", MetricCategory.Gpu, MetricUnit.Celsius);
    }

    private void EnsureBaselinePanel(string panelKey, string title, MetricCategory category, MetricUnit unit)
    {
        var panel = GetOrCreatePanel(panelKey, title, category, unit);
        panel.IsNewlyCreated = false;
    }

    private void UpdateSummaryCards(MetricSnapshot snapshot, PanelDetailContext detailContext)
    {
        var lookup = snapshot.Samples
            .GroupBy(sample => $"{sample.PanelKey}:{sample.SeriesKey}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var cpuUsage = lookup.GetValueOrDefault("cpu-total:usage")?.Value ?? 0d;
        var cpuFrequency = lookup.GetValueOrDefault("cpu-frequency:clock")?.Value ?? 0d;
        SummaryCards[0].Update(
            $"{cpuUsage:0.#}%",
            BuildSummaryCaption(
                cpuFrequency > 0 ? $"{cpuFrequency / 1000d:0.00} GHz" : "Processor load",
                detailContext.ProcessCount > 0 ? $"{FormatCompactCount(detailContext.ProcessCount)} proc" : null,
                detailContext.ThreadCount > 0 ? $"{FormatCompactCount(detailContext.ThreadCount)} thr" : null));

        var usedMemory = lookup.GetValueOrDefault("memory:used-gb")?.Value ?? 0d;
        var availableMemory = lookup.GetValueOrDefault("memory:available-gb")?.Value ?? 0d;
        var totalMemory = usedMemory + availableMemory;
        var memoryPct = totalMemory > 0 ? usedMemory / totalMemory * 100d : 0d;
        SummaryCards[1].Update(
            FormatCapacityForSummary(usedMemory),
            totalMemory > 0 ? $"{memoryPct:0.#}% of {FormatCapacityForSummary(totalMemory)}" : "Memory in play");

        var diskRead = snapshot.Samples.Where(sample => sample.Category == MetricCategory.Disk && sample.SeriesKey == "read").Sum(sample => sample.Value);
        var diskWrite = snapshot.Samples.Where(sample => sample.Category == MetricCategory.Disk && sample.SeriesKey == "write").Sum(sample => sample.Value);
        SummaryCards[2].Update(
            $"{diskRead + diskWrite:0.0} MB/s",
            $"{diskRead:0.0} read / {diskWrite:0.0} write");

        var netDown = snapshot.Samples.Where(sample => sample.Category == MetricCategory.Network && sample.SeriesKey == "download").Sum(sample => sample.Value);
        var netUp = snapshot.Samples.Where(sample => sample.Category == MetricCategory.Network && sample.SeriesKey == "upload").Sum(sample => sample.Value);
        SummaryCards[3].Update(
            $"{netDown:0.0} Mbps",
            $"{netUp:0.0} up");
    }

    private static TimeRangePreset MapToRangePreset(int minutes) => minutes switch
    {
        <= 1 => TimeRangePreset.OneMinute,
        <= 5 => TimeRangePreset.FiveMinutes,
        <= 15 => TimeRangePreset.FifteenMinutes,
        <= 30 => TimeRangePreset.ThirtyMinutes,
        <= 60 => TimeRangePreset.OneHour,
        <= 720 => TimeRangePreset.TwelveHours,
        <= 1440 => TimeRangePreset.TwentyFourHours,
        <= 10080 => TimeRangePreset.SevenDays,
        _ => TimeRangePreset.ThirtyDays,
    };

    private static int ParseOptionalInt(string? text, int fallback, int minValue, int maxValue)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return int.TryParse(text.Trim(), out var parsed) && parsed >= minValue && parsed <= maxValue
            ? parsed
            : fallback;
    }

    private static string FormatCapacityForSummary(double gigabytes)
    {
        if (gigabytes >= 1024d)
        {
            return $"{gigabytes / 1024d:0.0} TiB";
        }

        return $"{gigabytes:0.0} GiB";
    }

    private static string BuildSummaryCaption(params string?[] parts) =>
        string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string FormatCompactCount(double value)
    {
        value = Math.Max(0d, value);
        if (value >= 1_000_000d)
        {
            return $"{value / 1_000_000d:0.#}M";
        }

        if (value >= 1_000d)
        {
            return $"{value / 1_000d:0.#}k";
        }

        return $"{value:0}";
    }
}

public sealed class SelectionOption
{
    public SelectionOption(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public int Value { get; }

    public string Label { get; }
}

public sealed class ThemeSelectionOption
{
    public ThemeSelectionOption(ThemeMode value, string label)
    {
        Value = value;
        Label = label;
    }

    public ThemeMode Value { get; }

    public string Label { get; }
}

public sealed class SummaryCardViewModel : ObservableObject
{
    private string _value = "--";
    private string _caption = "Waiting for data";

    public SummaryCardViewModel(string glyph, string title, Brush accentBrush)
    {
        Glyph = glyph;
        Title = title;
        AccentBrush = accentBrush;
    }

    public string Glyph { get; }

    public string Title { get; }

    public Brush AccentBrush { get; }

    public string Value
    {
        get => _value;
        private set => SetProperty(ref _value, value);
    }

    public string Caption
    {
        get => _caption;
        private set => SetProperty(ref _caption, value);
    }

    public void Update(string value, string caption)
    {
        Value = value;
        Caption = caption;
    }
}

public sealed class PanelToggleViewModel : ObservableObject
{
    private bool _isVisible;

    public PanelToggleViewModel(string panelKey, string title, bool isVisible)
    {
        PanelKey = panelKey;
        Title = title;
        _isVisible = isVisible;
    }

    public string PanelKey { get; }

    public string Title { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

internal sealed class PanelDetailContext
{
    public static PanelDetailContext Empty { get; } = new(0d, 0, 0, 0, Array.Empty<ProcessActivitySample>(), new Dictionary<string, DriveDetailContext>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyList<ProcessActivitySample> _processes;
    private readonly IReadOnlyDictionary<string, DriveDetailContext> _driveDetails;

    public PanelDetailContext(
        double cpuFrequencyMhz,
        int processCount,
        int threadCount,
        int handleCount,
        IReadOnlyList<ProcessActivitySample> processes,
        IReadOnlyDictionary<string, DriveDetailContext> driveDetails)
    {
        CpuFrequencyMhz = cpuFrequencyMhz;
        ProcessCount = processCount;
        ThreadCount = threadCount;
        HandleCount = handleCount;
        _processes = processes;
        _driveDetails = driveDetails;
    }

    public double CpuFrequencyMhz { get; }

    public int ProcessCount { get; }

    public int ThreadCount { get; }

    public int HandleCount { get; }

    public IReadOnlyList<ProcessActivitySample> Processes => _processes;

    public bool TryGetDriveDetail(string panelKey, out DriveDetailContext detail)
    {
        detail = default;
        var suffix = ExtractDriveSuffix(panelKey);
        return !string.IsNullOrWhiteSpace(suffix) && _driveDetails.TryGetValue(suffix, out detail);
    }

    public static PanelDetailContext FromSnapshot(MetricSnapshot snapshot)
    {
        var lookup = snapshot.Samples
            .GroupBy(sample => $"{sample.PanelKey}:{sample.SeriesKey}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        var driveDetails = snapshot.Samples
            .Where(sample => sample.PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase))
            .GroupBy(sample => ExtractDriveSuffix(sample.PanelKey), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key!,
                group => new DriveDetailContext(
                    group.LastOrDefault(sample => string.Equals(sample.SeriesKey, "used-percent", StringComparison.OrdinalIgnoreCase))?.Value ?? 0d,
                    group.LastOrDefault(sample => string.Equals(sample.SeriesKey, "used-gb", StringComparison.OrdinalIgnoreCase))?.Value ?? 0d,
                    group.LastOrDefault(sample => string.Equals(sample.SeriesKey, "total-gb", StringComparison.OrdinalIgnoreCase))?.Value ?? 0d),
                StringComparer.OrdinalIgnoreCase);

        return new PanelDetailContext(
            lookup.GetValueOrDefault("cpu-frequency:clock"),
            (int)Math.Round(lookup.GetValueOrDefault("host-activity:processes"), MidpointRounding.AwayFromZero),
            (int)Math.Round(lookup.GetValueOrDefault("host-activity:threads"), MidpointRounding.AwayFromZero),
            (int)Math.Round(lookup.GetValueOrDefault("host-activity:handles"), MidpointRounding.AwayFromZero),
            snapshot.LiveDetails?.Processes ?? Array.Empty<ProcessActivitySample>(),
            driveDetails);
    }

    private static string ExtractDriveSuffix(string panelKey)
    {
        if (panelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase))
        {
            return panelKey["disk-".Length..];
        }

        if (panelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase))
        {
            return panelKey["volume-".Length..];
        }

        return string.Empty;
    }
}

internal readonly record struct DriveDetailContext(double UsedPercent, double UsedGigabytes, double TotalGigabytes);

public enum ProcessSortMode
{
    Highest = 0,
    Lowest = 1,
    Name = 2,
}

public sealed class ProcessListItemViewModel
{
    public ProcessListItemViewModel(string key, string name, string value, string caption, double intensity)
    {
        Key = key;
        Name = name;
        Value = value;
        Caption = caption;
        Intensity = intensity;
    }

    public string Key { get; }

    public string Name { get; }

    public string Value { get; }

    public string Caption { get; }

    public double Intensity { get; }
}

public sealed class MetricPanelViewModel : ObservableObject
{
    private static readonly TimeSpan BufferTrimInterval = TimeSpan.FromSeconds(20);
    private readonly Dictionary<string, SeriesBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SolidColorBrush _accentBrush;

    private IReadOnlyList<ChartSeriesViewModel> _visibleSeries = Array.Empty<ChartSeriesViewModel>();
    private string _currentValue = "Waiting";
    private string _secondaryValue = "Collecting hardware samples";
    private string _footerText = "arming";
    private string _scaleLabel = string.Empty;
    private string _emptyStateText = "Waiting for samples";
    private double _chartCeilingValue;
    private bool _isVisible;
    private TimeRangePreset _selectedRange = TimeRangePreset.FifteenMinutes;
    private DateTimeOffset _windowStartUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
    private DateTimeOffset _windowEndUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _zoomWindowStartUtc;
    private DateTimeOffset? _zoomWindowEndUtc;
    private DateTimeOffset _latestPointTimestampUtc = DateTimeOffset.UtcNow;
    private PanelDetailContext _detailContext = PanelDetailContext.Empty;
    private IReadOnlyList<ProcessListItemViewModel> _processRows = Array.Empty<ProcessListItemViewModel>();
    private ProcessSortMode _processSortMode = ProcessSortMode.Highest;
    private TimeSpan _retentionWindow = TimeSpan.FromHours(VaktrConfig.DefaultMaxRetentionHours);

    public MetricPanelViewModel(string panelKey, string title, MetricCategory category, MetricUnit unit)
    {
        PanelKey = panelKey;
        Title = title;
        Category = category;
        Unit = unit;
        _accentBrush = ResolveBrush(0);
    }

    public string PanelKey { get; }

    public string Title { get; }

    public MetricCategory Category { get; }

    public MetricUnit Unit { get; }

    public bool IsNewlyCreated { get; set; }

    public bool IsDashboardPanel => true;

    public int SortBucket => PanelKey switch
    {
        "cpu-total" => 0,
        "cpu-cores" => 1,
        "cpu-frequency" => 2,
        "cpu-temperature" => 3,
        "gpu-total" => 4,
        "gpu-memory" => 5,
        "gpu-temperature" => 6,
        "host-activity" => 7,
        "memory" => 8,
        _ when PanelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase) || PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase) => 9,
        _ when PanelKey.StartsWith("net-", StringComparison.OrdinalIgnoreCase) => 10,
        _ => 11,
    };

    public int SortVariant => PanelKey switch
    {
        _ when PanelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase) => 0,
        _ when PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase) => 1,
        _ => 0,
    };

    public string SortGroupKey => PanelKey switch
    {
        _ when PanelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase) => PanelKey["disk-".Length..],
        _ when PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase) => PanelKey["volume-".Length..],
        _ => PanelKey,
    };

    public string Badge => Category switch
    {
        MetricCategory.Disk when PrefersGaugeVisual => "DRV",
        MetricCategory.Cpu => "CPU",
        MetricCategory.Gpu => "GPU",
        MetricCategory.Memory => "MEM",
        MetricCategory.Disk => "DSK",
        MetricCategory.Network => "NET",
        MetricCategory.System => "SYS",
        _ => "LIVE",
    };

    public Brush AccentBrush => _accentBrush;

    public bool PrefersGaugeVisual =>
        Category == MetricCategory.Disk &&
        PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase);

    public bool SupportsProcessTable =>
        string.Equals(PanelKey, "cpu-total", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(PanelKey, "memory", StringComparison.OrdinalIgnoreCase);

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public TimeRangePreset SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (!SetProperty(ref _selectedRange, value))
            {
                return;
            }

            _zoomWindowStartUtc = null;
            _zoomWindowEndUtc = null;
            RefreshPresentation();
        }
    }

    public IReadOnlyList<ChartSeriesViewModel> VisibleSeries
    {
        get => _visibleSeries;
        private set => SetProperty(ref _visibleSeries, value);
    }

    public string CurrentValue
    {
        get => _currentValue;
        private set => SetProperty(ref _currentValue, value);
    }

    public string SecondaryValue
    {
        get => _secondaryValue;
        private set => SetProperty(ref _secondaryValue, value);
    }

    public string FooterText
    {
        get => _footerText;
        private set => SetProperty(ref _footerText, value);
    }

    public string ScaleLabel
    {
        get => _scaleLabel;
        private set => SetProperty(ref _scaleLabel, value);
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    public double ChartCeilingValue
    {
        get => _chartCeilingValue;
        private set => SetProperty(ref _chartCeilingValue, value);
    }

    public double GaugeValue =>
        !PrefersGaugeVisual || VisibleSeries.Count == 0 || VisibleSeries[0].Points.Count == 0
            ? 0d
            : Math.Clamp(VisibleSeries[0].Points[^1].Value, 0d, 100d);

    public IReadOnlyList<ProcessListItemViewModel> ProcessRows
    {
        get => _processRows;
        private set => SetProperty(ref _processRows, value);
    }

    public ProcessSortMode ProcessSortMode
    {
        get => _processSortMode;
        set
        {
            if (!SetProperty(ref _processSortMode, value))
            {
                return;
            }

            RefreshProcessRows();
        }
    }

    public string ProcessTableTitle =>
        string.Equals(PanelKey, "memory", StringComparison.OrdinalIgnoreCase)
            ? "Process memory"
            : "Process load";

    public event EventHandler? ExpandRequested;

    public bool IsZoomed => _zoomWindowStartUtc.HasValue && _zoomWindowEndUtc.HasValue;

    public DateTimeOffset WindowStartUtc
    {
        get => _windowStartUtc;
        private set => SetProperty(ref _windowStartUtc, value);
    }

    public DateTimeOffset WindowEndUtc
    {
        get => _windowEndUtc;
        private set => SetProperty(ref _windowEndUtc, value);
    }

    public void LoadHistory(MetricSeriesHistory history)
    {
        _buffers.Clear();
        var paletteIndex = 0;
        var latestTimestamp = DateTimeOffset.MinValue;
        foreach (var series in history.Series)
        {
            _buffers[series.SeriesKey] = new SeriesBuffer(
                series.SeriesKey,
                series.SeriesName,
                series.Points.ToList(),
                ResolveBrush(paletteIndex),
                ResolveFillBrush(paletteIndex),
                series.Points.LastOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow);
            if (series.Points.Count > 0 && series.Points[^1].Timestamp > latestTimestamp)
            {
                latestTimestamp = series.Points[^1].Timestamp;
            }
            paletteIndex++;
        }

        if (latestTimestamp > DateTimeOffset.MinValue)
        {
            _latestPointTimestampUtc = latestTimestamp;
        }

        TrimBuffers(DateTimeOffset.UtcNow);
        RefreshPresentation();
    }

    public void AppendSample(MetricSample sample)
    {
        if (!_buffers.TryGetValue(sample.SeriesKey, out var buffer))
        {
            var paletteIndex = _buffers.Count;
            buffer = new SeriesBuffer(
                sample.SeriesKey,
                sample.SeriesName,
                [],
                ResolveBrush(paletteIndex),
                ResolveFillBrush(paletteIndex),
                sample.Timestamp);
            _buffers.Add(sample.SeriesKey, buffer);
        }

        buffer.Points.Add(new MetricPoint(sample.Timestamp, sample.Value));
        _latestPointTimestampUtc = sample.Timestamp;
        if (sample.Timestamp - buffer.LastTrimUtc >= BufferTrimInterval)
        {
            TrimBuffers(sample.Timestamp);
        }

        RefreshPresentation(sample.Timestamp);
    }

    public void SetRetentionWindow(TimeSpan retentionWindow)
    {
        var normalizedWindow = retentionWindow <= TimeSpan.Zero
            ? TimeSpan.FromHours(VaktrConfig.DefaultMaxRetentionHours)
            : retentionWindow;

        if (_retentionWindow == normalizedWindow)
        {
            return;
        }

        _retentionWindow = normalizedWindow;
        TrimBuffers(DateTimeOffset.UtcNow);
        RefreshPresentation();
    }

    internal void ApplyDetailContext(PanelDetailContext detailContext)
    {
        var processDetailsChanged =
            !ReferenceEquals(_detailContext.Processes, detailContext.Processes) ||
            _detailContext.ProcessCount != detailContext.ProcessCount ||
            _detailContext.ThreadCount != detailContext.ThreadCount ||
            _detailContext.HandleCount != detailContext.HandleCount;

        _detailContext = detailContext;
        if (processDetailsChanged && SupportsProcessTable)
        {
            RefreshProcessRows();
        }

        UpdateSummaryText();
    }

    public PanelHoverInfo? BuildHoverInfo(double normalizedPosition)
    {
        if (VisibleSeries.Count == 0)
        {
            return null;
        }

        normalizedPosition = Math.Clamp(normalizedPosition, 0d, 1d);
        var referenceSeries = VisibleSeries.OrderByDescending(series => series.Points.Count).FirstOrDefault();
        if (referenceSeries is null || referenceSeries.Points.Count == 0)
        {
            return null;
        }

        var index = (int)Math.Round(normalizedPosition * Math.Max(0, referenceSeries.Points.Count - 1));
        index = Math.Clamp(index, 0, referenceSeries.Points.Count - 1);
        var targetTimestamp = referenceSeries.Points[index].Timestamp;

        var values = VisibleSeries
            .Select(series =>
            {
                if (series.Points.Count == 0)
                {
                    return null;
                }

                var seriesIndex = (int)Math.Round(normalizedPosition * Math.Max(0, series.Points.Count - 1));
                var point = series.Points[Math.Clamp(seriesIndex, 0, series.Points.Count - 1)];
                return new HoverSeriesValue(series.Name, FormatValue(point.Value, Unit), series.StrokeBrush);
            })
            .Where(value => value is not null)
            .Cast<HoverSeriesValue>()
            .ToArray();

        return values.Length == 0 ? null : new PanelHoverInfo(targetTimestamp, values);
    }

    public void RequestExpand() => ExpandRequested?.Invoke(this, EventArgs.Empty);

    public void ApplyRangePreset(TimeRangePreset preset)
    {
        _zoomWindowStartUtc = null;
        _zoomWindowEndUtc = null;
        if (_selectedRange != preset)
        {
            SelectedRange = preset;
            return;
        }

        RefreshPresentation();
    }

    public void ZoomToWindow(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (endUtc <= startUtc)
        {
            return;
        }

        _zoomWindowStartUtc = startUtc;
        _zoomWindowEndUtc = endUtc;
        RefreshPresentation();
    }

    public void ResetZoom()
    {
        if (!IsZoomed)
        {
            return;
        }

        _zoomWindowStartUtc = null;
        _zoomWindowEndUtc = null;
        RefreshPresentation();
    }

    private void RefreshPresentation(DateTimeOffset? anchor = null)
    {
        var liveAnchor = anchor ?? _latestPointTimestampUtc;
        var end = IsZoomed ? _zoomWindowEndUtc!.Value : liveAnchor;
        var start = IsZoomed ? _zoomWindowStartUtc!.Value : end.AddMinutes(-(int)SelectedRange);
        WindowStartUtc = start;
        WindowEndUtc = end;

        var pointBudget = ResolveVisiblePointBudget(end - start);
        var visibleSeries = new List<ChartSeriesViewModel>(_buffers.Count);
        foreach (var buffer in _buffers.Values)
        {
            if (!ShouldShowSeries(buffer))
            {
                continue;
            }

            var points = BuildVisiblePoints(buffer.Points, start, end, pointBudget);
            if (points.Count == 0)
            {
                continue;
            }

            visibleSeries.Add(new ChartSeriesViewModel(buffer.Name, points, buffer.StrokeBrush, buffer.FillBrush));
        }

        VisibleSeries = visibleSeries.ToArray();
        UpdateSummaryText();
        FooterText = BuildFooterText(start, end);
    }

    private void UpdateSummaryText()
    {
        var latestByKey = _buffers
            .Where(entry => entry.Value.Points.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Points[^1].Value,
                StringComparer.OrdinalIgnoreCase);

        if (VisibleSeries.Count == 0)
        {
            if (string.Equals(PanelKey, "cpu-temperature", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(PanelKey, "gpu-temperature", StringComparison.OrdinalIgnoreCase))
            {
                CurrentValue = "Not detected";
                SecondaryValue = "No readable GPU sensor";
                EmptyStateText = "GPU temperature sensor not detected on this system";
            }
            else
            {
                CurrentValue = "Waiting";
                SecondaryValue = "Collecting hardware samples";
                EmptyStateText = "Waiting for samples";
            }

            ScaleLabel = string.Empty;
            ChartCeilingValue = Unit == MetricUnit.Percent ? 100d : Unit == MetricUnit.Count ? 100d : 0d;
            return;
        }

        EmptyStateText = "Waiting for samples";

        var latestBySeries = VisibleSeries.ToDictionary(
            series => series.Name,
            series => series.Points.Last().Value,
            StringComparer.OrdinalIgnoreCase);

        switch (Category)
        {
            case MetricCategory.Cpu when string.Equals(PanelKey, "cpu-total", StringComparison.OrdinalIgnoreCase):
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Usage"):0.#}%";
                SecondaryValue = BuildJoinedText(
                    _detailContext.CpuFrequencyMhz > 0 ? $"{_detailContext.CpuFrequencyMhz / 1000d:0.00} GHz" : "Total processor load",
                    _detailContext.ProcessCount > 0 ? $"{FormatCompactCount(_detailContext.ProcessCount)} proc" : null,
                    _detailContext.ThreadCount > 0 ? $"{FormatCompactCount(_detailContext.ThreadCount)} thr" : null);
                ScaleLabel = _detailContext.HandleCount > 0
                    ? $"100% ceiling · {FormatCompactCount(_detailContext.HandleCount)} handles"
                    : "100% ceiling";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Cpu:
                var averageCore = latestBySeries.Count > 0 ? latestBySeries.Values.Average() : 0d;
                if (string.Equals(PanelKey, "cpu-frequency", StringComparison.OrdinalIgnoreCase))
                {
                    var clockMhz = latestBySeries.GetValueOrDefault("Clock");
                    CurrentValue = $"{clockMhz / 1000d:0.00} GHz";
                    SecondaryValue = BuildJoinedText(
                        "Processor frequency",
                        _detailContext.ProcessCount > 0 ? $"{FormatCompactCount(_detailContext.ProcessCount)} proc" : null,
                        _detailContext.ThreadCount > 0 ? $"{FormatCompactCount(_detailContext.ThreadCount)} thr" : null);
                    ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, Math.Max(1000d, clockMhz));
                    ScaleLabel = _detailContext.HandleCount > 0
                        ? $"{FormatCompactCount(_detailContext.HandleCount)} handles"
                        : $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                    break;
                }

                if (string.Equals(PanelKey, "cpu-temperature", StringComparison.OrdinalIgnoreCase))
                {
                    var cpuTemp = latestBySeries.GetValueOrDefault("Temperature");
                    CurrentValue = $"{cpuTemp:0.#} C";
                    SecondaryValue = "Processor temperature";
                    ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 100d);
                    ScaleLabel = $"Max {FormatValue(ChartCeilingValue, Unit)}";
                    break;
                }

                CurrentValue = $"{averageCore:0.#}% avg";
                SecondaryValue = BuildJoinedText(
                    latestBySeries.Count == 1 ? "1 core lane active" : $"{latestBySeries.Count} core lanes active",
                    _detailContext.CpuFrequencyMhz > 0 ? $"{_detailContext.CpuFrequencyMhz / 1000d:0.00} GHz" : null);
                ScaleLabel = $"100% per core / {latestBySeries.Count} cores";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Memory:
                var used = latestBySeries.GetValueOrDefault("Used");
                var available = latestBySeries.GetValueOrDefault("Available");
                var total = used + available;
                var percent = total > 0 ? used / total * 100d : 0d;
                CurrentValue = $"{FormatCapacity(used)} used";
                SecondaryValue = BuildJoinedText(
                    $"{percent:0.#}% of {FormatCapacity(total)}",
                    _detailContext.ProcessCount > 0 ? $"{FormatCompactCount(_detailContext.ProcessCount)} proc" : null,
                    _detailContext.ThreadCount > 0 ? $"{FormatCompactCount(_detailContext.ThreadCount)} thr" : null);
                ScaleLabel = $"{FormatCapacity(total)} total";
                ChartCeilingValue = Math.Max(total, 1d);
                break;
            case MetricCategory.Gpu when string.Equals(PanelKey, "gpu-memory", StringComparison.OrdinalIgnoreCase):
                var dedicated = latestBySeries.GetValueOrDefault("Dedicated");
                CurrentValue = $"{FormatCapacity(dedicated)} used";
                SecondaryValue = "Dedicated graphics memory";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, Math.Max(1d, dedicated));
                ScaleLabel = $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                break;
            case MetricCategory.Gpu when string.Equals(PanelKey, "gpu-temperature", StringComparison.OrdinalIgnoreCase):
                var gpuTemp = latestBySeries.GetValueOrDefault("Temperature");
                CurrentValue = $"{gpuTemp:0.#} C";
                SecondaryValue = "Graphics temperature";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 100d);
                ScaleLabel = $"Max {FormatValue(ChartCeilingValue, Unit)}";
                break;
            case MetricCategory.Gpu:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Usage"):0.#}%";
                SecondaryValue = "GPU usage";
                ChartCeilingValue = 100d;
                ScaleLabel = "100% ceiling";
                break;
            case MetricCategory.Disk when PrefersGaugeVisual:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Used"):0.#}% used";
                var totalGb = latestByKey.GetValueOrDefault("total-gb");
                var usedGb = latestByKey.GetValueOrDefault("used-gb");
                SecondaryValue = totalGb > 0 ? $"{FormatCapacity(usedGb)} of {FormatCapacity(totalGb)}" : "Drive capacity";
                ScaleLabel = totalGb > 0 ? $"{FormatCapacity(totalGb)} total" : "100% ceiling";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Disk:
                _detailContext.TryGetDriveDetail(PanelKey, out var driveDetail);
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Read"):0.0} MB/s read";
                SecondaryValue = BuildJoinedText(
                    $"{latestBySeries.GetValueOrDefault("Write"):0.0} MB/s write",
                    driveDetail.TotalGigabytes > 0 ? $"{driveDetail.UsedPercent:0.#}% full" : null);
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 10d);
                ScaleLabel = driveDetail.TotalGigabytes > 0
                    ? $"Peak {FormatValue(ChartCeilingValue, Unit)} · {FormatCapacity(driveDetail.TotalGigabytes)} total"
                    : $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                break;
            case MetricCategory.Network:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Down"):0.0} Mbps down";
                SecondaryValue = $"{latestBySeries.GetValueOrDefault("Up"):0.0} Mbps up";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 10d);
                ScaleLabel = $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                break;
            case MetricCategory.System:
                CurrentValue = $"{FormatCompactCount(latestBySeries.GetValueOrDefault("Processes"))} proc";
                SecondaryValue = BuildJoinedText(
                    $"{FormatCompactCount(latestBySeries.GetValueOrDefault("Threads"))} thr",
                    $"{FormatCompactCount(latestBySeries.GetValueOrDefault("Handles"))} handles");
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 100d);
                ScaleLabel = $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                break;
            default:
                CurrentValue = FormatValue(VisibleSeries[0].Points.Last().Value, Unit);
                SecondaryValue = "Live metric";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 1d);
                ScaleLabel = $"Peak {FormatValue(ChartCeilingValue, Unit)}";
                break;
        }
    }

    private bool ShouldShowSeries(SeriesBuffer buffer)
    {
        if (!PrefersGaugeVisual)
        {
            return true;
        }

        return string.Equals(buffer.Key, "used-percent", StringComparison.OrdinalIgnoreCase);
    }

    private void TrimBuffers(DateTimeOffset anchor)
    {
        var keepAfter = anchor - _retentionWindow;
        foreach (var buffer in _buffers.Values)
        {
            buffer.Points.RemoveAll(point => point.Timestamp < keepAfter);
            buffer.LastTrimUtc = anchor;
        }
    }

    private void RefreshProcessRows()
    {
        if (!SupportsProcessTable)
        {
            ProcessRows = Array.Empty<ProcessListItemViewModel>();
            return;
        }

        IEnumerable<ProcessActivitySample> processes = _detailContext.Processes;
        var peakMemory = Math.Max(0.1d, _detailContext.Processes.Count > 0 ? _detailContext.Processes.Max(process => process.MemoryGigabytes) : 0d);
        processes = ProcessSortMode switch
        {
            ProcessSortMode.Name => processes.OrderBy(process => process.Name, StringComparer.OrdinalIgnoreCase),
            ProcessSortMode.Lowest when string.Equals(PanelKey, "memory", StringComparison.OrdinalIgnoreCase) =>
                processes.OrderBy(process => process.MemoryGigabytes).ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase),
            ProcessSortMode.Lowest =>
                processes.OrderBy(process => process.CpuPercent).ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase),
            _ when string.Equals(PanelKey, "memory", StringComparison.OrdinalIgnoreCase) =>
                processes.OrderByDescending(process => process.MemoryGigabytes).ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase),
            _ =>
                processes.OrderByDescending(process => process.CpuPercent).ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase),
        };

        ProcessRows = processes
            .Select(process => CreateProcessRow(process, peakMemory))
            .ToArray();
    }

    private ProcessListItemViewModel CreateProcessRow(ProcessActivitySample process, double peakMemory)
    {
        if (string.Equals(PanelKey, "memory", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessListItemViewModel(
                $"{process.ProcessId}",
                process.Name,
                FormatCapacity(process.MemoryGigabytes),
                $"{process.CpuPercent:0.#}% cpu / {FormatCompactCount(process.ThreadCount)} thr / {FormatCompactCount(process.HandleCount)} handles",
                Math.Clamp(process.MemoryGigabytes / peakMemory, 0d, 1d));
        }

        return new ProcessListItemViewModel(
            $"{process.ProcessId}",
            process.Name,
            $"{process.CpuPercent:0.#}%",
            $"{FormatCapacity(process.MemoryGigabytes)} ram / {FormatCompactCount(process.ThreadCount)} thr / {FormatCompactCount(process.HandleCount)} handles",
            Math.Clamp(process.CpuPercent / 100d, 0d, 1d));
    }

    private SolidColorBrush ResolveBrush(int index)
    {
        var color = Category switch
        {
            MetricCategory.Cpu when index == 0 => BrushFactory.ParseColor("#5DE6FF"),
            MetricCategory.Cpu => index % 2 == 0 ? BrushFactory.ParseColor("#3BB7FF") : BrushFactory.ParseColor("#8BF7FF"),
            MetricCategory.Memory when index == 0 => BrushFactory.ParseColor("#7BF7D0"),
            MetricCategory.Memory => BrushFactory.ParseColor("#54C99B"),
            MetricCategory.Gpu when index == 0 => BrushFactory.ParseColor("#6DA8FF"),
            MetricCategory.Gpu => BrushFactory.ParseColor("#8D7BFF"),
            MetricCategory.Disk when index == 0 => BrushFactory.ParseColor("#FF9B54"),
            MetricCategory.Disk => BrushFactory.ParseColor("#FFD166"),
            MetricCategory.Network when index == 0 => BrushFactory.ParseColor("#9A8CFF"),
            MetricCategory.Network => BrushFactory.ParseColor("#6E9BFF"),
            MetricCategory.System when index == 0 => BrushFactory.ParseColor("#79C7FF"),
            MetricCategory.System => BrushFactory.ParseColor("#4FA3FF"),
            _ => BrushFactory.ParseColor("#7AD8FF"),
        };

        return new SolidColorBrush(color);
    }

    private SolidColorBrush ResolveFillBrush(int index)
    {
        var color = ResolveBrush(index).Color;
        return new SolidColorBrush(ColorHelper.FromArgb(56, color.R, color.G, color.B));
    }

    private static string FormatValue(double value, MetricUnit unit) => unit switch
    {
        MetricUnit.Percent => $"{value:0.#}%",
        MetricUnit.Celsius => $"{value:0.#} C",
        MetricUnit.Gigabytes => FormatCapacity(value),
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        MetricUnit.Count => FormatCompactCount(value),
        _ => $"{value:0.##}",
    };

    private static string BuildJoinedText(params string?[] parts)
    {
        var filtered = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return filtered.Length == 0 ? "Live metric" : string.Join(" / ", filtered);
    }

    private static string FormatCompactCount(double value)
    {
        value = Math.Max(0d, value);
        if (value >= 1_000_000d)
        {
            return $"{value / 1_000_000d:0.#}M";
        }

        if (value >= 1_000d)
        {
            return $"{value / 1_000d:0.#}k";
        }

        return $"{value:0}";
    }

    private static string FormatCapacity(double gigabytes)
    {
        if (gigabytes >= 1024d)
        {
            return $"{gigabytes / 1024d:0.0} TiB";
        }

        return $"{gigabytes:0.0} GiB";
    }

    private static double ResolveDynamicCeiling(IReadOnlyList<ChartSeriesViewModel> series, double minimum)
    {
        var peak = series.SelectMany(item => item.Points).DefaultIfEmpty(new MetricPoint(DateTimeOffset.UtcNow, minimum)).Max(point => point.Value);
        return Math.Max(minimum, peak * 1.12d);
    }

    private string BuildFooterText(DateTimeOffset start, DateTimeOffset end)
    {
        if (IsZoomed)
        {
            var span = end - start;
            var spanText = span.TotalMinutes >= 1d
                ? $"{span.TotalMinutes:0.#}m"
                : $"{span.TotalSeconds:0.#}s";
            return PrefersGaugeVisual
                ? $"capacity / zoomed {spanText}"
                : $"zoomed {spanText}";
        }

        return PrefersGaugeVisual
            ? $"capacity / {FormatRangeLabel((int)SelectedRange)} replay"
            : $"{FormatRangeLabel((int)SelectedRange)} replay";
    }

    private static IReadOnlyList<MetricPoint> BuildVisiblePoints(
        IReadOnlyList<MetricPoint> points,
        DateTimeOffset start,
        DateTimeOffset end,
        int pointBudget)
    {
        if (points.Count == 0)
        {
            return Array.Empty<MetricPoint>();
        }

        var windowPoints = new List<MetricPoint>(Math.Min(points.Count, pointBudget));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point.Timestamp < start)
            {
                continue;
            }

            if (point.Timestamp > end)
            {
                break;
            }

            windowPoints.Add(point);
        }

        if (windowPoints.Count <= pointBudget)
        {
            return windowPoints.ToArray();
        }

        var sampled = new MetricPoint[pointBudget];
        var step = (windowPoints.Count - 1d) / (pointBudget - 1d);
        for (var index = 0; index < pointBudget; index++)
        {
            var sourceIndex = (int)Math.Round(index * step);
            sampled[index] = windowPoints[Math.Clamp(sourceIndex, 0, windowPoints.Count - 1)];
        }

        return sampled;
    }

    private static int ResolveVisiblePointBudget(TimeSpan window) => window switch
    {
        _ when window >= TimeSpan.FromDays(30) => 240,
        _ when window >= TimeSpan.FromDays(7) => 300,
        _ when window >= TimeSpan.FromDays(1) => 380,
        _ when window >= TimeSpan.FromHours(12) => 460,
        _ when window >= TimeSpan.FromHours(1) => 560,
        _ => 680,
    };

    private static string FormatRangeLabel(int minutes)
    {
        if (minutes >= 43200)
        {
            return "30d";
        }

        if (minutes >= 10080)
        {
            return "7d";
        }

        if (minutes >= 1440)
        {
            return "24h";
        }

        if (minutes >= 720)
        {
            return "12h";
        }

        if (minutes >= 60)
        {
            return $"{minutes / 60}h";
        }

        return $"{minutes}m";
    }

    private sealed class SeriesBuffer
    {
        public SeriesBuffer(
            string key,
            string name,
            List<MetricPoint> points,
            SolidColorBrush strokeBrush,
            SolidColorBrush fillBrush,
            DateTimeOffset lastTrimUtc)
        {
            Key = key;
            Name = name;
            Points = points;
            StrokeBrush = strokeBrush;
            FillBrush = fillBrush;
            LastTrimUtc = lastTrimUtc;
        }

        public string Key { get; }

        public string Name { get; }

        public List<MetricPoint> Points { get; }

        public SolidColorBrush StrokeBrush { get; }

        public SolidColorBrush FillBrush { get; }

        public DateTimeOffset LastTrimUtc { get; set; }
    }
}

public sealed class ChartSeriesViewModel
{
    public ChartSeriesViewModel(string name, IReadOnlyList<MetricPoint> points, Brush strokeBrush, Brush fillBrush)
    {
        Name = name;
        Points = points;
        StrokeBrush = strokeBrush;
        FillBrush = fillBrush;
    }

    public string Name { get; }

    public IReadOnlyList<MetricPoint> Points { get; }

    public Brush StrokeBrush { get; }

    public Brush FillBrush { get; }
}

public sealed class PanelHoverInfo
{
    public PanelHoverInfo(DateTimeOffset timestamp, IReadOnlyList<HoverSeriesValue> values)
    {
        Timestamp = timestamp;
        Values = values;
    }

    public DateTimeOffset Timestamp { get; }

    public IReadOnlyList<HoverSeriesValue> Values { get; }
}

public sealed class HoverSeriesValue
{
    public HoverSeriesValue(string label, string value, Brush brush)
    {
        Label = label;
        Value = value;
        Brush = brush;
    }

    public string Label { get; }

    public string Value { get; }

    public Brush Brush { get; }
}

internal static class BrushFactory
{
    public static SolidColorBrush CreateBrush(string hex) => new(ParseColor(hex));

    public static Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 6)
        {
            value = $"FF{value}";
        }

        return ColorHelper.FromArgb(
            Convert.ToByte(value[0..2], 16),
            Convert.ToByte(value[2..4], 16),
            Convert.ToByte(value[4..6], 16),
            Convert.ToByte(value[6..8], 16));
    }
}
