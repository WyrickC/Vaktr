using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Vaktr.Core.Models;

namespace Vaktr.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, MetricPanelViewModel> _panelLookup = new(StringComparer.OrdinalIgnoreCase);

    private string _storageDirectory = string.Empty;
    private string _scrapeIntervalInput = string.Empty;
    private string _retentionHoursInput = string.Empty;
    private int _selectedIntervalSeconds = VaktrConfig.DefaultScrapeIntervalSeconds;
    private int _selectedWindowMinutes = 15;
    private ThemeMode _selectedTheme = ThemeMode.Dark;
    private bool _launchOnStartup;
    private bool _minimizeToTray = true;
    private bool _isSettingsOpen;
    private string _statusText = "Arming telemetry deck";
    private MetricPanelViewModel? _expandedPanel;

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
            new SelectionOption(1, "1 minute"),
            new SelectionOption(5, "5 minutes"),
            new SelectionOption(15, "15 minutes"),
            new SelectionOption(60, "1 hour"),
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
            Theme = SelectedTheme,
            StorageDirectory = EffectiveStorageDirectory,
            LaunchOnStartup = LaunchOnStartup,
            MinimizeToTray = MinimizeToTray,
            PanelVisibility = visibility,
        }.Normalize();
    }

    public void ApplyConfig(VaktrConfig config)
    {
        var normalized = config.Normalize();
        StorageDirectory = string.Equals(normalized.StorageDirectory, VaktrConfig.DefaultStorageDirectory, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized.StorageDirectory;
        ScrapeIntervalInput = normalized.ScrapeIntervalSeconds == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : normalized.ScrapeIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RetentionHoursInput = normalized.MaxRetentionHours == VaktrConfig.DefaultMaxRetentionHours
            ? string.Empty
            : normalized.MaxRetentionHours.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SelectedIntervalSeconds = normalized.ScrapeIntervalSeconds;
        SelectedWindowMinutes = normalized.GraphWindowMinutes;
        SelectedTheme = normalized.Theme;
        LaunchOnStartup = normalized.LaunchOnStartup;
        MinimizeToTray = normalized.MinimizeToTray;

        foreach (var toggle in PanelToggles)
        {
            toggle.IsVisible = normalized.PanelVisibility.GetValueOrDefault(toggle.PanelKey, true);
        }

        foreach (var panel in _panelLookup.Values)
        {
            panel.IsVisible = normalized.PanelVisibility.GetValueOrDefault(panel.PanelKey, panel.IsDashboardPanel);
            panel.SelectedRange = MapToRangePreset(normalized.GraphWindowMinutes);
        }

        SyncDashboardPanels();
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

        StatusText = $"Last sample {snapshot.Timestamp.LocalDateTime:t}";
        UpdateSummaryCards(snapshot);

        if (panelCreated)
        {
            SyncPanelToggles();
        }

        SyncDashboardPanels();
    }

    public void ApplyPanelVisibility()
    {
        var visibilityMap = PanelToggles.ToDictionary(toggle => toggle.PanelKey, toggle => toggle.IsVisible, StringComparer.OrdinalIgnoreCase);
        foreach (var panel in _panelLookup.Values)
        {
            panel.IsVisible = visibilityMap.GetValueOrDefault(panel.PanelKey, panel.IsDashboardPanel);
        }

        SyncDashboardPanels();
    }

    public int EffectiveScrapeIntervalSeconds =>
        ParseOptionalInt(ScrapeIntervalInput, VaktrConfig.DefaultScrapeIntervalSeconds, 1, 60);

    public int EffectiveRetentionHours =>
        ParseOptionalInt(RetentionHoursInput, VaktrConfig.DefaultMaxRetentionHours, 1, 24 * 3650);

    public string EffectiveStorageDirectory =>
        string.IsNullOrWhiteSpace(StorageDirectory)
            ? VaktrConfig.DefaultStorageDirectory
            : StorageDirectory.Trim();

    private MetricPanelViewModel GetOrCreatePanel(string panelKey, string title, MetricCategory category, MetricUnit unit)
    {
        if (_panelLookup.TryGetValue(panelKey, out var existing))
        {
            return existing;
        }

        var panel = new MetricPanelViewModel(panelKey, title, category, unit)
        {
            SelectedRange = MapToRangePreset(SelectedWindowMinutes),
            IsVisible = !string.Equals(panelKey, "cpu-frequency", StringComparison.OrdinalIgnoreCase),
            IsNewlyCreated = true,
        };

        _panelLookup.Add(panelKey, panel);
        return panel;
    }

    private void SyncDashboardPanels()
    {
        var orderedPanels = _panelLookup.Values
            .Where(panel => panel.IsDashboardPanel && panel.IsVisible)
            .OrderBy(panel => panel.SortOrder)
            .ThenBy(panel => panel.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (DashboardPanels.Count == orderedPanels.Length && DashboardPanels.SequenceEqual(orderedPanels))
        {
            return;
        }

        DashboardPanels.Clear();
        foreach (var panel in orderedPanels)
        {
            DashboardPanels.Add(panel);
        }
    }

    private void SyncPanelToggles()
    {
        var visibilityLookup = PanelToggles.ToDictionary(toggle => toggle.PanelKey, toggle => toggle.IsVisible, StringComparer.OrdinalIgnoreCase);
        var panels = _panelLookup.Values
            .Where(panel => panel.IsDashboardPanel)
            .OrderBy(panel => panel.SortOrder)
            .ThenBy(panel => panel.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (PanelToggles.Count == panels.Length &&
            PanelToggles.Select(toggle => toggle.PanelKey).SequenceEqual(panels.Select(panel => panel.PanelKey), StringComparer.OrdinalIgnoreCase))
        {
            foreach (var toggle in PanelToggles)
            {
                toggle.IsVisible = visibilityLookup.GetValueOrDefault(toggle.PanelKey, true);
            }

            return;
        }

        PanelToggles.Clear();
        foreach (var panel in panels)
        {
            PanelToggles.Add(new PanelToggleViewModel(panel.PanelKey, panel.Title, visibilityLookup.GetValueOrDefault(panel.PanelKey, panel.IsVisible)));
        }
    }

    private void UpdateSummaryCards(MetricSnapshot snapshot)
    {
        var lookup = snapshot.Samples
            .GroupBy(sample => $"{sample.PanelKey}:{sample.SeriesKey}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var cpuUsage = lookup.GetValueOrDefault("cpu-total:usage")?.Value ?? 0d;
        var cpuFrequency = lookup.GetValueOrDefault("cpu-frequency:clock")?.Value ?? 0d;
        SummaryCards[0].Update(
            $"{cpuUsage:0.#}%",
            cpuFrequency > 0 ? $"{cpuFrequency / 1000d:0.00} GHz live" : "Processor load");

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
        _ => TimeRangePreset.OneHour,
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

public sealed class MetricPanelViewModel : ObservableObject
{
    private static readonly TimeSpan LiveBufferWindow = TimeSpan.FromMinutes(75);
    private readonly Dictionary<string, SeriesBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SolidColorBrush _accentBrush;

    private IReadOnlyList<ChartSeriesViewModel> _visibleSeries = Array.Empty<ChartSeriesViewModel>();
    private string _currentValue = "Waiting";
    private string _secondaryValue = "Collecting hardware samples";
    private string _footerText = "arming";
    private string _scaleLabel = string.Empty;
    private double _chartCeilingValue;
    private bool _isVisible;
    private TimeRangePreset _selectedRange = TimeRangePreset.FifteenMinutes;
    private DateTimeOffset _windowStartUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
    private DateTimeOffset _windowEndUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _zoomWindowStartUtc;
    private DateTimeOffset? _zoomWindowEndUtc;

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

    public bool IsDashboardPanel => !string.Equals(PanelKey, "cpu-frequency", StringComparison.OrdinalIgnoreCase);

    public int SortOrder => PanelKey switch
    {
        "cpu-total" => 0,
        "cpu-cores" => 1,
        "memory" => 2,
        _ when PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase) => 3,
        _ when PanelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase) => 4,
        _ when PanelKey.StartsWith("net-", StringComparison.OrdinalIgnoreCase) => 5,
        _ => 6,
    };

    public string Badge => Category switch
    {
        MetricCategory.Disk when PrefersGaugeVisual => "DRV",
        MetricCategory.Cpu => "CPU",
        MetricCategory.Memory => "MEM",
        MetricCategory.Disk => "DSK",
        MetricCategory.Network => "NET",
        _ => "LIVE",
    };

    public Brush AccentBrush => _accentBrush;

    public bool PrefersGaugeVisual =>
        Category == MetricCategory.Disk &&
        Unit == MetricUnit.Percent &&
        PanelKey.StartsWith("volume-", StringComparison.OrdinalIgnoreCase);

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

    public double ChartCeilingValue
    {
        get => _chartCeilingValue;
        private set => SetProperty(ref _chartCeilingValue, value);
    }

    public double GaugeValue =>
        !PrefersGaugeVisual || VisibleSeries.Count == 0 || VisibleSeries[0].Points.Count == 0
            ? 0d
            : Math.Clamp(VisibleSeries[0].Points[^1].Value, 0d, 100d);

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
        foreach (var series in history.Series)
        {
            _buffers[series.SeriesKey] = new SeriesBuffer(series.SeriesKey, series.SeriesName, series.Points.ToList());
        }

        RefreshPresentation();
    }

    public void AppendSample(MetricSample sample)
    {
        if (!_buffers.TryGetValue(sample.SeriesKey, out var buffer))
        {
            buffer = new SeriesBuffer(sample.SeriesKey, sample.SeriesName, []);
            _buffers.Add(sample.SeriesKey, buffer);
        }

        buffer.Points.Add(new MetricPoint(sample.Timestamp, sample.Value));
        var keepAfter = sample.Timestamp - LiveBufferWindow;
        buffer.Points.RemoveAll(point => point.Timestamp < keepAfter);

        RefreshPresentation(sample.Timestamp);
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
        var latestPoint = _buffers.Values.SelectMany(buffer => buffer.Points).LastOrDefault();
        var liveAnchor = anchor ?? latestPoint?.Timestamp ?? DateTimeOffset.UtcNow;
        var end = IsZoomed ? _zoomWindowEndUtc!.Value : liveAnchor;
        var start = IsZoomed ? _zoomWindowStartUtc!.Value : end.AddMinutes(-(int)SelectedRange);
        WindowStartUtc = start;
        WindowEndUtc = end;

        var visibleSeries = _buffers.Values
            .Where(ShouldShowSeries)
            .Select((buffer, index) =>
            {
                var points = buffer.Points.Where(point => point.Timestamp >= start && point.Timestamp <= end).ToArray();
                return points.Length == 0
                    ? null
                    : new ChartSeriesViewModel(buffer.Name, points, ResolveBrush(index), ResolveFillBrush(index));
            })
            .Where(series => series is not null)
            .Cast<ChartSeriesViewModel>()
            .ToArray();

        VisibleSeries = visibleSeries;
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
            CurrentValue = "Waiting";
            SecondaryValue = "Collecting hardware samples";
            ScaleLabel = string.Empty;
            ChartCeilingValue = Unit == MetricUnit.Percent ? 100d : 0d;
            return;
        }

        var latestBySeries = VisibleSeries.ToDictionary(
            series => series.Name,
            series => series.Points.Last().Value,
            StringComparer.OrdinalIgnoreCase);

        switch (Category)
        {
            case MetricCategory.Cpu when string.Equals(PanelKey, "cpu-total", StringComparison.OrdinalIgnoreCase):
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Usage"):0.#}%";
                SecondaryValue = "Total processor load";
                ScaleLabel = "Ceiling 100%";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Cpu:
                var averageCore = latestBySeries.Count > 0 ? latestBySeries.Values.Average() : 0d;
                CurrentValue = $"{averageCore:0.#}% avg";
                SecondaryValue = latestBySeries.Count == 1 ? "1 core lane active" : $"{latestBySeries.Count} core lanes active";
                ScaleLabel = $"100% per core // {latestBySeries.Count} cores";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Memory:
                var used = latestBySeries.GetValueOrDefault("Used");
                var available = latestBySeries.GetValueOrDefault("Available");
                var total = used + available;
                var percent = total > 0 ? used / total * 100d : 0d;
                CurrentValue = $"{FormatCapacity(used)} used";
                SecondaryValue = $"{percent:0.#}% of {FormatCapacity(total)}";
                ScaleLabel = $"{FormatCapacity(total)} total";
                ChartCeilingValue = Math.Max(total, 1d);
                break;
            case MetricCategory.Disk when PrefersGaugeVisual:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Used"):0.#}% used";
                var totalGb = latestByKey.GetValueOrDefault("total-gb");
                var usedGb = latestByKey.GetValueOrDefault("used-gb");
                SecondaryValue = totalGb > 0 ? $"{FormatCapacity(usedGb)} of {FormatCapacity(totalGb)}" : "Drive capacity";
                ScaleLabel = totalGb > 0 ? $"{FormatCapacity(totalGb)} total" : "Ceiling 100%";
                ChartCeilingValue = 100d;
                break;
            case MetricCategory.Disk:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Read"):0.0} MB/s read";
                SecondaryValue = $"{latestBySeries.GetValueOrDefault("Write"):0.0} MB/s write";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 10d);
                ScaleLabel = $"Peak scale {FormatValue(ChartCeilingValue, Unit)}";
                break;
            case MetricCategory.Network:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Down"):0.0} Mbps down";
                SecondaryValue = $"{latestBySeries.GetValueOrDefault("Up"):0.0} Mbps up";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 10d);
                ScaleLabel = $"Peak scale {FormatValue(ChartCeilingValue, Unit)}";
                break;
            default:
                CurrentValue = FormatValue(VisibleSeries[0].Points.Last().Value, Unit);
                SecondaryValue = "Live metric";
                ChartCeilingValue = ResolveDynamicCeiling(VisibleSeries, 1d);
                ScaleLabel = $"Peak scale {FormatValue(ChartCeilingValue, Unit)}";
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

    private SolidColorBrush ResolveBrush(int index)
    {
        var color = Category switch
        {
            MetricCategory.Cpu when index == 0 => BrushFactory.ParseColor("#5DE6FF"),
            MetricCategory.Cpu => index % 2 == 0 ? BrushFactory.ParseColor("#3BB7FF") : BrushFactory.ParseColor("#8BF7FF"),
            MetricCategory.Memory when index == 0 => BrushFactory.ParseColor("#7BF7D0"),
            MetricCategory.Memory => BrushFactory.ParseColor("#54C99B"),
            MetricCategory.Disk when index == 0 => BrushFactory.ParseColor("#FF9B54"),
            MetricCategory.Disk => BrushFactory.ParseColor("#FFD166"),
            MetricCategory.Network when index == 0 => BrushFactory.ParseColor("#9A8CFF"),
            MetricCategory.Network => BrushFactory.ParseColor("#6E9BFF"),
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
        MetricUnit.Gigabytes => FormatCapacity(value),
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        _ => $"{value:0.##}",
    };

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
                ? $"capacity // zoomed {spanText}"
                : $"zoomed {spanText}";
        }

        return PrefersGaugeVisual
            ? $"capacity // {(int)SelectedRange}m replay"
            : $"{(int)SelectedRange}m replay";
    }

    private sealed class SeriesBuffer
    {
        public SeriesBuffer(string key, string name, List<MetricPoint> points)
        {
            Key = key;
            Name = name;
            Points = points;
        }

        public string Key { get; }

        public string Name { get; }

        public List<MetricPoint> Points { get; }
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
