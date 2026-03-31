using System.Collections.Specialized;
using System.ComponentModel;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.App;

public sealed class ShellWindow : Window
{
    private static readonly TimeSpan StartupYieldDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromMilliseconds(600);

    private readonly MainViewModel _viewModel;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;

    private readonly Grid _rootLayout;
    private readonly StackPanel _summaryHost;
    private readonly StackPanel _panelHost;
    private readonly TextBlock _statusText;

    private CollectorService? _collectorService;
    private bool _initialized;
    private DateTimeOffset _lastUiRefreshUtc = DateTimeOffset.MinValue;

    public ShellWindow(
        MainViewModel viewModel,
        IMetricStore metricStore,
        IConfigStore configStore,
        AutoLaunchService autoLaunchService)
    {
        StartupTrace.Write("ShellWindow ctor start // minimal-v4");
        _viewModel = viewModel;
        _metricStore = metricStore;
        _configStore = configStore;
        _autoLaunchService = autoLaunchService;

        Title = "Vaktr";

        _statusText = CreateSecondaryText(string.Empty, 12);
        _summaryHost = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 14,
        };

        _panelHost = new StackPanel
        {
            Spacing = 14,
        };

        _rootLayout = BuildRootLayout();
        Content = _rootLayout;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        Closed += OnWindowClosed;

        UpdateStatusText();
        RenderStartupPlaceholders();
        ApplyTheme(_viewModel.SelectedTheme);
        StartupTrace.Write("ShellWindow ctor complete // minimal-v4");
    }

    public void ApplyTheme(ThemeMode mode)
    {
        _rootLayout.RequestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout // minimal-v4");
        var shellBorder = new Border
        {
            Background = ResolveBrush("ShellBackgroundBrush", "#0B1622"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1E3144"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(20),
            Child = BuildShellStack(),
        };

        var root = new Grid
        {
            Background = ResolveBrush("AppBackdropBrush", "#061018"),
        };

        root.Children.Add(new ScrollViewer
        {
            Content = new Grid
            {
                Margin = new Thickness(18),
                Children =
                {
                    shellBorder,
                },
            },
        });

        root.Loaded += OnRootLoaded;
        return root;
    }

    private StackPanel BuildShellStack()
    {
        StartupTrace.Write("BuildShellStack // minimal-v4");
        return new StackPanel
        {
            Spacing = 20,
            Children =
            {
                BuildHeader(),
                BuildControlsSurface(),
                CreateSectionHeader("AT A GLANCE", "Local machine stats only. Fast startup, low overhead, and simple reads."),
                BuildSummarySurface(),
                CreateSectionHeader("LIVE PANELS", "Current panel values with compact series snapshots."),
                _panelHost,
                BuildFooter(),
            },
        };
    }

    private Grid BuildHeader()
    {
        StartupTrace.Write("BuildHeader // minimal-v4");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateAccentText("RIG TELEMETRY // LOCAL ONLY", 12, 100),
                CreatePrimaryText("Vaktr", 30, true),
                CreateSecondaryText("WinUI 3 monitor for CPU, memory, disk, and network with a lightweight local history."),
            },
        };

        var statusBorder = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 10, 12, 10),
            Child = _statusText,
        };

        grid.Children.Add(titleStack);
        grid.Children.Add(statusBorder);
        Grid.SetColumn(statusBorder, 1);
        return grid;
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // minimal-v4");
        var themeButton = CreateGhostButton("Theme");
        themeButton.Click += OnThemeQuickToggle;

        var applyButton = CreateFilledButton("Apply");
        applyButton.Click += OnSaveSettingsClick;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateMutedText("Settings are intentionally lean in this stability cut."),
                CreateSecondaryText("Theme, retention, interval, and storage still come from the existing config/view model."),
            },
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                themeButton,
                applyButton,
            },
        };
        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16),
            Child = grid,
        };
    }

    private UIElement BuildSummarySurface()
    {
        StartupTrace.Write("BuildSummarySurface // minimal-v4");
        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16),
            Child = _summaryHost,
        };
    }

    private Border BuildFooter()
    {
        StartupTrace.Write("BuildFooter // minimal-v4");
        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateAccentText("LOW OVERHEAD", 11, 80),
                    CreateSecondaryText("Vaktr samples locally, keeps only a bounded live window in memory, and stores history in SQLite."),
                },
            },
        };
    }

    private void BuildSummaryCards()
    {
        StartupTrace.Write("BuildSummaryCards // minimal-v4");
        _summaryHost.Children.Clear();
        foreach (var card in _viewModel.SummaryCards)
        {
            _summaryHost.Children.Add(new Border
            {
                Background = ResolveBrush("SurfaceBrush", "#102131"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        CreateMutedText(card.Title, 11),
                        CreatePrimaryText(card.Value, 24, true),
                        CreateSecondaryText(card.Caption, 12),
                    },
                },
            });
        }
    }

    private void RefreshDashboardPanels()
    {
        StartupTrace.Write("RefreshDashboardPanels // minimal-v4");
        _panelHost.Children.Clear();

        var panels = _viewModel.DashboardPanels.ToArray();
        if (panels.Length == 0)
        {
            _panelHost.Children.Add(new Border
            {
                Background = ResolveBrush("SurfaceBrush", "#102131"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18),
                Child = CreateSecondaryText("Waiting for telemetry panels."),
            });
            return;
        }

        foreach (var panel in panels)
        {
            _panelHost.Children.Add(CreatePanelCard(panel));
        }
    }

    private Border CreatePanelCard(MetricPanelViewModel panel)
    {
        var rangeButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        rangeButtons.Children.Add(CreateRangeButton("1m", panel, TimeRangePreset.OneMinute));
        rangeButtons.Children.Add(CreateRangeButton("5m", panel, TimeRangePreset.FiveMinutes));
        rangeButtons.Children.Add(CreateRangeButton("15m", panel, TimeRangePreset.FifteenMinutes));
        rangeButtons.Children.Add(CreateRangeButton("1h", panel, TimeRangePreset.OneHour));

        var seriesHost = new StackPanel
        {
            Spacing = 8,
        };

        if (panel.VisibleSeries.Count == 0)
        {
            seriesHost.Children.Add(CreateMutedText("Waiting for samples", 12));
        }
        else
        {
            foreach (var series in panel.VisibleSeries)
            {
                var latestValue = series.Points.Count == 0
                    ? "--"
                    : FormatValue(series.Points[^1].Value, panel.Unit);

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(CreateSecondaryText(series.Name, 12));

                var valueText = new TextBlock
                {
                    FontFamily = new FontFamily("Bahnschrift"),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
                    Text = latestValue,
                };
                row.Children.Add(valueText);
                Grid.SetColumn(valueText, 1);

                seriesHost.Children.Add(new Border
                {
                    Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                    BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 10, 12, 10),
                    Child = row,
                });
            }
        }

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    CreateAccentText(panel.Badge, 11, 80),
                    CreatePrimaryText(panel.Title, 22, true),
                    CreatePrimaryText(panel.CurrentValue, 24, true),
                    CreateSecondaryText(panel.SecondaryValue),
                    rangeButtons,
                    seriesHost,
                },
            },
        };
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        StartupTrace.Write("OnRootLoaded start // minimal-v4");

        try
        {
            await Task.Delay(StartupYieldDelay);
            StartupTrace.Write("OnRootLoaded resumed after first paint // minimal-v4");
            _viewModel.StatusText = "Loading local history";
            UpdateStatusText();

            var config = _viewModel.BuildConfig();
            await _metricStore.InitializeAsync(config, CancellationToken.None);

            var historyWindow = TimeSpan.FromMinutes(Math.Max(config.GraphWindowMinutes, 5));
            var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.Subtract(historyWindow), CancellationToken.None);
            _viewModel.LoadHistory(history);

            RefreshUiIfDue(force: true);
            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);

            _collectorService = new CollectorService(new WindowsMetricCollector(), _metricStore);
            _collectorService.SnapshotCollected += OnSnapshotCollected;

            _viewModel.StatusText = "Starting telemetry";
            UpdateStatusText();
            await _collectorService.StartAsync(config, CancellationToken.None);

            _viewModel.StatusText = "Streaming local telemetry";
            UpdateStatusText();
            StartupTrace.Write("OnRootLoaded complete // minimal-v4");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("OnRootLoaded", ex);
            _viewModel.StatusText = $"Startup issue: {ex.Message}";
            UpdateStatusText();
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged -= OnDashboardPanelsChanged;

        if (_collectorService is not null)
        {
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
            await _collectorService.DisposeAsync();
        }
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.StatusText = "Applying settings";
            UpdateStatusText();

            var config = _viewModel.BuildConfig();
            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await _configStore.SaveAsync(config, CancellationToken.None);

            if (_collectorService is null)
            {
                _collectorService = new CollectorService(new WindowsMetricCollector(), _metricStore);
                _collectorService.SnapshotCollected += OnSnapshotCollected;
            }

            await _collectorService.StartAsync(config, CancellationToken.None);
            _viewModel.StatusText = "Settings applied";
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Settings issue: {ex.Message}";
            UpdateStatusText();
        }
    }

    private void OnThemeQuickToggle(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedTheme = _viewModel.SelectedTheme == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;

        App.CurrentApp.ApplyTheme(_viewModel.SelectedTheme);
    }

    private void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _viewModel.ApplySnapshot(snapshot);
            UpdateStatusText();
            RefreshUiIfDue();
        });
    }

    private void OnDashboardPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDashboardPanels();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.StatusText), StringComparison.Ordinal))
        {
            UpdateStatusText();
        }
    }

    private void UpdateStatusText()
    {
        _statusText.Text = _viewModel.StatusText;
    }

    private void RenderStartupPlaceholders()
    {
        StartupTrace.Write("RenderStartupPlaceholders // minimal-v4");
        _summaryHost.Children.Clear();
        _summaryHost.Children.Add(CreatePlaceholderCard("CPU", "Waiting for first sample"));
        _summaryHost.Children.Add(CreatePlaceholderCard("Memory", "Loading local history"));

        _panelHost.Children.Clear();
        _panelHost.Children.Add(CreatePlaceholderCard("Live panels", "Window is up. Telemetry warmup starts after first paint."));
    }

    private void RefreshUiIfDue(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastUiRefreshUtc < UiRefreshInterval)
        {
            return;
        }

        BuildSummaryCards();
        RefreshDashboardPanels();
        _lastUiRefreshUtc = now;
    }

    private Border CreatePlaceholderCard(string title, string text) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateMutedText(title, 11),
                    CreateSecondaryText(text, 12),
                },
            },
        };

    private Button CreateRangeButton(string text, MetricPanelViewModel panel, TimeRangePreset preset)
    {
        var button = CreateGhostButton(text);
        button.Opacity = panel.SelectedRange == preset ? 1 : 0.65;
        button.Click += (_, _) =>
        {
            panel.SelectedRange = preset;
            RefreshDashboardPanels();
        };

        return button;
    }

    private static Border CreateSectionHeader(string eyebrow, string text) =>
        new()
        {
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateAccentText(eyebrow, 11, 80),
                    CreateSecondaryText(text, 13),
                },
            },
        };

    private static Button CreateFilledButton(string text) =>
        new()
        {
            Content = text,
            Background = ResolveBrush("AccentSoftBrush", "#10394D"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 40,
        };

    private static Button CreateGhostButton(string text) =>
        new()
        {
            Content = text,
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 40,
        };

    private static TextBlock CreatePrimaryText(string text, double fontSize, bool semiBold) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            FontWeight = semiBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateSecondaryText(string text, double fontSize = 14) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateMutedText(string text, double fontSize = 12) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateAccentText(string text, double fontSize, int characterSpacing) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
        };

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
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
}
