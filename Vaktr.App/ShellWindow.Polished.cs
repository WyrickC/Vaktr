using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Vaktr.App.Controls;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.App;

public sealed partial class ShellWindow : Window
{
    private static readonly TimeSpan StartupYieldDelay = TimeSpan.FromMilliseconds(30);

    private readonly MainViewModel _viewModel;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly Dictionary<string, TelemetryPanelCard> _panelCards = new(StringComparer.OrdinalIgnoreCase);

    private readonly Grid _rootLayout;
    private readonly StackPanel _controlsBodyHost;
    private readonly StackPanel _summaryHost;
    private readonly Grid _dashboardGrid;
    private readonly StackPanel _panelToggleHost;
    private readonly TextBlock _statusText;

    private CollectorService? _collectorService;
    private bool _dashboardRefreshQueued;
    private bool _initialized;

    public ShellWindow(
        MainViewModel viewModel,
        IMetricStore metricStore,
        IConfigStore configStore,
        AutoLaunchService autoLaunchService)
    {
        StartupTrace.Write("ShellWindow ctor start // polished-v10");
        _viewModel = viewModel;
        _metricStore = metricStore;
        _configStore = configStore;
        _autoLaunchService = autoLaunchService;

        Title = "Vaktr";

        _statusText = CreateSecondaryText(string.Empty, 12);
        _controlsBodyHost = new StackPanel
        {
            Spacing = 14,
        };
        _summaryHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
        };
        _dashboardGrid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 18,
        };
        _panelToggleHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };

        _rootLayout = BuildRootLayout();
        Content = _rootLayout;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged += OnPanelTogglesChanged;
        Closed += OnWindowClosed;

        RenderStartupControls();
        RenderSummaryPlaceholders();
        RefreshDashboardPanels();
        UpdateStatusText();
        ApplyTheme(_viewModel.SelectedTheme);
        StartupTrace.Write("ShellWindow ctor complete // polished-v10");
    }

    public void ApplyTheme(ThemeMode mode)
    {
        _rootLayout.RequestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    private void BuildSummaryCards()
    {
        StartupTrace.Write("BuildSummaryCards // polished-v10");
        _summaryHost.Children.Clear();
        foreach (var card in _viewModel.SummaryCards)
        {
            var glyphText = new TextBlock
            {
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            glyphText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Glyph)) });
            glyphText.SetBinding(TextBlock.ForegroundProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.AccentBrush)) });

            var glow = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(16),
                Opacity = 0.18,
            };
            glow.SetBinding(Border.BackgroundProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.AccentBrush)) });

            var titleText = CreateMutedText(string.Empty, 11);
            titleText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Title)) });

            var valueText = CreatePrimaryText(string.Empty, 26, true);
            valueText.FontFamily = new FontFamily("Bahnschrift");
            valueText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Value)) });

            var captionText = CreateSecondaryText(string.Empty, 12);
            captionText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Caption)) });

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new Grid
            {
                Width = 46,
                Height = 46,
                Children =
                {
                    glow,
                    glyphText,
                },
            });

            var details = new StackPanel
            {
                Margin = new Thickness(14, 0, 0, 0),
                Spacing = 4,
                Children =
                {
                    titleText,
                    valueText,
                    captionText,
                },
            };
            row.Children.Add(details);
            Grid.SetColumn(details, 1);

            _summaryHost.Children.Add(new Border
            {
                DataContext = card,
                Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14),
                Width = 250,
                Child = row,
            });
        }
    }

    private void RefreshPanelToggles()
    {
        StartupTrace.Write("RefreshPanelToggles // polished-v10");
    }

    private void RefreshDashboardPanels()
    {
        StartupTrace.Write("RefreshDashboardPanels // polished-v10");
        var panels = _viewModel.DashboardPanels.ToArray();
        var activeKeys = new HashSet<string>(panels.Select(panel => panel.PanelKey), StringComparer.OrdinalIgnoreCase);
        foreach (var staleKey in _panelCards.Keys.Where(key => !activeKeys.Contains(key)).ToArray())
        {
            _panelCards.Remove(staleKey);
        }

        foreach (var panel in panels)
        {
            if (!_panelCards.TryGetValue(panel.PanelKey, out var card))
            {
                card = new TelemetryPanelCard { Panel = panel };
                _panelCards.Add(panel.PanelKey, card);
            }
            else
            {
                card.Panel = panel;
            }
        }

        _dashboardGrid.Children.Clear();
        _dashboardGrid.RowDefinitions.Clear();
        _dashboardGrid.ColumnDefinitions.Clear();

        if (panels.Length == 0)
        {
            _dashboardGrid.Children.Add(CreatePlaceholderCard("Live board", "Telemetry wakes up after the first local sample arrives."));
            return;
        }

        var columns = DetermineDashboardColumns();
        for (var column = 0; column < columns; column++)
        {
            _dashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rows = (int)Math.Ceiling(panels.Length / (double)columns);
        for (var row = 0; row < rows; row++)
        {
            _dashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < panels.Length; index++)
        {
            var card = _panelCards[panels[index].PanelKey];
            _dashboardGrid.Children.Add(card);
            Grid.SetColumn(card, index % columns);
            Grid.SetRow(card, index / columns);
        }
    }

    private int DetermineDashboardColumns()
    {
        var width = _rootLayout.ActualWidth > 0 ? _rootLayout.ActualWidth : 1280;
        return width >= 1800 ? 3 : width >= 1120 ? 2 : 1;
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        StartupTrace.Write("OnRootLoaded start // polished-v10");

        try
        {
            await Task.Delay(StartupYieldDelay);
            var config = _viewModel.BuildConfig();
            StartupTrace.Write("OnRootLoaded resumed after first paint // polished-v10");
            RenderAdvancedControls();
            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await TryLoadHistoryAsync(config);
            await EnsureCollectorRunningAsync(config);
            App.CurrentApp.MarkStartupSettled();
            StartupTrace.Write("OnRootLoaded complete // polished-v10");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("OnRootLoaded", ex);
            _viewModel.StatusText = $"Startup issue: {ex.Message}";
            UpdateStatusText();
            App.CurrentApp.MarkStartupSettled();
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged -= OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged -= OnPanelTogglesChanged;

        if (_collectorService is not null)
        {
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
            _collectorService.CollectionFailed -= OnCollectionFailed;
            await _collectorService.DisposeAsync();
        }
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.StatusText = "Applying settings";
            UpdateStatusText();

            _viewModel.ApplyPanelVisibility();
            var config = _viewModel.BuildConfig();
            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await _configStore.SaveAsync(config, CancellationToken.None);

            if (_collectorService is null)
            {
                _collectorService = new CollectorService(new WindowsMetricCollector(), _metricStore);
                _collectorService.SnapshotCollected += OnSnapshotCollected;
                _collectorService.CollectionFailed += OnCollectionFailed;
            }

            await _collectorService.StartAsync(config, CancellationToken.None);
            _viewModel.StatusText = _viewModel.DashboardPanels.Count > 0 ? "Streaming local telemetry" : "Waiting for first sample";
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
            BuildSummaryCards();
            UpdateStatusText();
        });
    }

    private void OnCollectionFailed(object? sender, Exception ex)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_viewModel.DashboardPanels.Count == 0)
            {
                _viewModel.StatusText = $"Telemetry retrying: {ex.Message}";
                UpdateStatusText();
            }
        });
    }

    private void QueueDashboardRefresh()
    {
        if (_dashboardRefreshQueued)
        {
            return;
        }

        _dashboardRefreshQueued = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _dashboardRefreshQueued = false;
            if (!_initialized)
            {
                return;
            }

            StartupTrace.Write("Queued dashboard refresh after resize // polished-v10");
            RefreshDashboardPanels();
        });
    }

    private void OnRootLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        QueueDashboardRefresh();
    }

    private void OnDashboardPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDashboardPanels();

    private void OnPanelTogglesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshPanelToggles();

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

    private void RenderSummaryPlaceholders()
    {
        _summaryHost.Children.Clear();
        _summaryHost.Children.Add(CreatePlaceholderCard("CPU", "Waiting for first sample"));
        _summaryHost.Children.Add(CreatePlaceholderCard("Memory", "Loading local history"));
        _summaryHost.Children.Add(CreatePlaceholderCard("Disk", "Preparing local boards"));
    }

    private async Task TryLoadHistoryAsync(VaktrConfig config)
    {
        try
        {
            _viewModel.StatusText = "Loading local history";
            UpdateStatusText();

            await _metricStore.InitializeAsync(config, CancellationToken.None);

            var historyWindow = TimeSpan.FromMinutes(Math.Max(config.GraphWindowMinutes, 5));
            var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.Subtract(historyWindow), CancellationToken.None);
            _viewModel.LoadHistory(history);
            BuildSummaryCards();
            RefreshDashboardPanels();
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("History load", ex);
            _viewModel.StatusText = "History unavailable, starting live telemetry";
            UpdateStatusText();
        }
    }

    private async Task EnsureCollectorRunningAsync(VaktrConfig config)
    {
        _collectorService ??= new CollectorService(new WindowsMetricCollector(), _metricStore);
        _collectorService.SnapshotCollected -= OnSnapshotCollected;
        _collectorService.CollectionFailed -= OnCollectionFailed;
        _collectorService.SnapshotCollected += OnSnapshotCollected;
        _collectorService.CollectionFailed += OnCollectionFailed;

        _viewModel.StatusText = "Starting telemetry";
        UpdateStatusText();
        await _collectorService.StartAsync(config, CancellationToken.None);

        _viewModel.StatusText = _viewModel.DashboardPanels.Count > 0 ? "Streaming local telemetry" : "Waiting for first sample";
        UpdateStatusText();
    }
}
