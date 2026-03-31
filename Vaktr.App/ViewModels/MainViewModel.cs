using System.Collections.ObjectModel;
using System.Windows.Media;
using Vaktr.Core.Models;

namespace Vaktr.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, MetricPanelViewModel> _panelLookup = new(StringComparer.OrdinalIgnoreCase);

    private string _storageDirectory = VaktrConfig.DefaultStorageDirectory;
    private int _selectedIntervalSeconds = 2;
    private int _selectedWindowMinutes = 15;
    private int _selectedRetentionDays = 30;
    private ThemeMode _selectedTheme = ThemeMode.Dark;
    private bool _launchOnStartup;
    private bool _minimizeToTray = true;
    private bool _isSettingsOpen;
    private string _statusText = "Awaiting first sample";
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
            new SummaryCardViewModel("CPU", "CPU", Brushes.Transparent),
            new SummaryCardViewModel("RAM", "Memory", Brushes.Transparent),
            new SummaryCardViewModel("DSK", "Disk", Brushes.Transparent),
            new SummaryCardViewModel("NET", "Network", Brushes.Transparent),
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

    public int SelectedRetentionDays
    {
        get => _selectedRetentionDays;
        set => SetProperty(ref _selectedRetentionDays, value);
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
            ScrapeIntervalSeconds = SelectedIntervalSeconds,
            GraphWindowMinutes = SelectedWindowMinutes,
            Retention = (RetentionPreset)SelectedRetentionDays,
            Theme = SelectedTheme,
            StorageDirectory = StorageDirectory,
            LaunchOnStartup = LaunchOnStartup,
            MinimizeToTray = MinimizeToTray,
            PanelVisibility = visibility,
        }.Normalize();
    }

    public void ApplyConfig(VaktrConfig config)
    {
        var normalized = config.Normalize();
        StorageDirectory = normalized.StorageDirectory;
        SelectedIntervalSeconds = normalized.ScrapeIntervalSeconds;
        SelectedWindowMinutes = normalized.GraphWindowMinutes;
        SelectedRetentionDays = (int)normalized.Retention;
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
            $"{usedMemory:0.0} GB",
            totalMemory > 0 ? $"{memoryPct:0.#}% of {totalMemory:0.0} GB" : "Memory usage");

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
    private readonly Dictionary<string, SeriesBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ChartSeriesViewModel> _visibleSeries = Array.Empty<ChartSeriesViewModel>();
    private string _currentValue = "Waiting";
    private string _secondaryValue = "Collecting hardware samples";
    private string _footerText = "Bootstrapping";
    private bool _isVisible;
    private TimeRangePreset _selectedRange = TimeRangePreset.FifteenMinutes;
    private DateTimeOffset _windowStartUtc = DateTimeOffset.UtcNow.AddMinutes(-15);
    private DateTimeOffset _windowEndUtc = DateTimeOffset.UtcNow;

    public MetricPanelViewModel(string panelKey, string title, MetricCategory category, MetricUnit unit)
    {
        PanelKey = panelKey;
        Title = title;
        Category = category;
        Unit = unit;
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
        _ when PanelKey.StartsWith("disk-", StringComparison.OrdinalIgnoreCase) => 3,
        _ when PanelKey.StartsWith("net-", StringComparison.OrdinalIgnoreCase) => 4,
        _ => 5,
    };

    public string Badge => Category switch
    {
        MetricCategory.Cpu => "CPU",
        MetricCategory.Memory => "MEM",
        MetricCategory.Disk => "DSK",
        MetricCategory.Network => "NET",
        _ => "LIVE",
    };

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
        var keepAfter = sample.Timestamp.AddHours(-2);
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
                var point = series.Points.OrderBy(point => Math.Abs((point.Timestamp - targetTimestamp).TotalMilliseconds)).FirstOrDefault();
                return point is null
                    ? null
                    : new HoverSeriesValue(series.Name, FormatValue(point.Value, Unit), series.StrokeBrush);
            })
            .Where(value => value is not null)
            .Cast<HoverSeriesValue>()
            .ToArray();

        return values.Length == 0 ? null : new PanelHoverInfo(targetTimestamp, values);
    }

    private void RefreshPresentation(DateTimeOffset? anchor = null)
    {
        var latestPoint = _buffers.Values.SelectMany(buffer => buffer.Points).LastOrDefault();
        var end = anchor ?? latestPoint?.Timestamp ?? DateTimeOffset.UtcNow;

        var start = end.AddMinutes(-(int)SelectedRange);
        WindowStartUtc = start;
        WindowEndUtc = end;

        var visibleSeries = _buffers.Values
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
        FooterText = $"{(int)SelectedRange}m window";
    }

    private void UpdateSummaryText()
    {
        if (VisibleSeries.Count == 0)
        {
            CurrentValue = "Waiting";
            SecondaryValue = "Collecting hardware samples";
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
                break;
            case MetricCategory.Cpu:
                var averageCore = latestBySeries.Count > 0 ? latestBySeries.Values.Average() : 0d;
                CurrentValue = $"{averageCore:0.#}% avg";
                SecondaryValue = $"{latestBySeries.Count} core traces active";
                break;
            case MetricCategory.Memory:
                var used = latestBySeries.GetValueOrDefault("Used");
                var available = latestBySeries.GetValueOrDefault("Available");
                var total = used + available;
                var percent = total > 0 ? used / total * 100d : 0d;
                CurrentValue = $"{used:0.0} GB used";
                SecondaryValue = $"{percent:0.#}% of {total:0.0} GB";
                break;
            case MetricCategory.Disk:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Read"):0.0} MB/s read";
                SecondaryValue = $"{latestBySeries.GetValueOrDefault("Write"):0.0} MB/s write";
                break;
            case MetricCategory.Network:
                CurrentValue = $"{latestBySeries.GetValueOrDefault("Down"):0.0} Mbps down";
                SecondaryValue = $"{latestBySeries.GetValueOrDefault("Up"):0.0} Mbps up";
                break;
            default:
                CurrentValue = FormatValue(VisibleSeries[0].Points.Last().Value, Unit);
                SecondaryValue = "Live metric";
                break;
        }
    }

    private SolidColorBrush ResolveBrush(int index)
    {
        var color = Category switch
        {
            MetricCategory.Cpu when index == 0 => (Color)ColorConverter.ConvertFromString("#6FD3FF"),
            MetricCategory.Cpu => index % 2 == 0
                ? (Color)ColorConverter.ConvertFromString("#44B9FF")
                : (Color)ColorConverter.ConvertFromString("#8BE6FF"),
            MetricCategory.Memory when index == 0 => (Color)ColorConverter.ConvertFromString("#9FE7FF"),
            MetricCategory.Memory => (Color)ColorConverter.ConvertFromString("#68C4FF"),
            MetricCategory.Disk when index == 0 => (Color)ColorConverter.ConvertFromString("#61C1FF"),
            MetricCategory.Disk => (Color)ColorConverter.ConvertFromString("#A6F0FF"),
            MetricCategory.Network when index == 0 => (Color)ColorConverter.ConvertFromString("#7AD8FF"),
            MetricCategory.Network => (Color)ColorConverter.ConvertFromString("#B7F4FF"),
            _ => (Color)ColorConverter.ConvertFromString("#7AD8FF"),
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private SolidColorBrush ResolveFillBrush(int index)
    {
        var color = ResolveBrush(index).Color;
        var fill = new SolidColorBrush(Color.FromArgb(56, color.R, color.G, color.B));
        fill.Freeze();
        return fill;
    }

    private static string FormatValue(double value, MetricUnit unit) => unit switch
    {
        MetricUnit.Percent => $"{value:0.#}%",
        MetricUnit.Gigabytes => $"{value:0.0} GB",
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        _ => $"{value:0.##}",
    };

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
