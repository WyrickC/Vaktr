using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Vaktr.App.Controls;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.App;

public sealed partial class ShellWindow : Window
{
    private static readonly int[] ScrapeIntervalPresets = [1, 2, 5, 10, 15, 30, 60];
    private static readonly int[] RetentionHourPresets = [1, 6, 12, 24, 48, 72, 168, 336, 720, 2160, 8760];
    private DeckEditorMode _activeDeckEditor = DeckEditorMode.Scrape;

    private readonly MainViewModel _viewModel;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly Dictionary<string, TelemetryPanelCard> _panelCards = new(StringComparer.OrdinalIgnoreCase);

    private readonly Grid _rootLayout;
    private readonly Border _controlsBodyHost;
    private readonly Border _brandHost;
    private readonly Grid _summaryHost;
    private readonly Grid _dashboardGrid;
    private readonly TextBlock _statusText;
    private readonly ActionChip _globalRangeButton;
    private readonly Border _globalRangeEditorHost;
    private readonly ActionChip _globalFiveMinuteButton;
    private readonly ActionChip _globalThirtyMinuteButton;
    private readonly ActionChip _globalOneHourButton;
    private readonly ActionChip _globalTwelveHourButton;
    private readonly ActionChip _globalTwentyFourHourButton;
    private readonly ActionChip _globalSevenDayButton;
    private readonly ActionChip _globalThirtyDayButton;
    private readonly ActionChip _globalResetZoomButton;

    private CollectorService? _collectorService;
    private nint _windowIconHandle;
    private bool _controlDeckEditableActive;
    private bool _dashboardRefreshQueued;
    private bool _hasReceivedFirstSnapshot;
    private bool _initialized;
    private int _lastDashboardColumnCount;
    private int _lastSummaryColumnCount;
    private bool _windowIconApplied;
    private bool _summaryCardsBound;
    private bool _globalRangeEditorVisible;
    private string _lastRenderedStatusText = string.Empty;
    private DateTimeOffset? _globalAbsoluteStartUtc;
    private DateTimeOffset? _globalAbsoluteEndUtc;

    public ShellWindow(
        MainViewModel viewModel,
        IMetricStore metricStore,
        IConfigStore configStore,
        AutoLaunchService autoLaunchService)
    {
        StartupTrace.Write("ShellWindow ctor start // polished-v19");
        _viewModel = viewModel;
        _metricStore = metricStore;
        _configStore = configStore;
        _autoLaunchService = autoLaunchService;

        Title = "Vaktr";

        _statusText = CreateSecondaryText(string.Empty, 12);
        _controlsBodyHost = new Border();
        _brandHost = CreateBrandPlaceholder();
        _summaryHost = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 14,
        };
        _dashboardGrid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 18,
        };
        _globalRangeButton = CreateActionChip("15m", OnToggleGlobalRangeEditor, true);
        _globalRangeButton.MinWidth = 124;
        _globalRangeEditorHost = new Border
        {
            Visibility = Visibility.Collapsed,
        };
        _globalFiveMinuteButton = CreateGlobalRangeChip("5m", 5);
        _globalThirtyMinuteButton = CreateGlobalRangeChip("30m", 30);
        _globalOneHourButton = CreateGlobalRangeChip("1h", 60);
        _globalTwelveHourButton = CreateGlobalRangeChip("12h", 720);
        _globalTwentyFourHourButton = CreateGlobalRangeChip("24h", 1440);
        _globalSevenDayButton = CreateGlobalRangeChip("7d", 10080);
        _globalThirtyDayButton = CreateGlobalRangeChip("30d", 43200);
        _globalResetZoomButton = CreateActionChip("Reset zoom", OnResetAllZoomClick);

        _rootLayout = BuildRootLayout();
        Content = _rootLayout;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged += OnPanelTogglesChanged;
        Closed += OnWindowClosed;
        Activated += OnWindowActivated;

        TryLoadBrandImage();
        RenderEditableControlDeck();
        BuildSummaryCards();
        RefreshDashboardPanels();
        RefreshGlobalRangeControls();
        UpdateStatusText();
        ApplyInitialTheme(_viewModel.SelectedTheme);
        StartupTrace.Write("ShellWindow ctor complete // polished-v19");
    }

    public void ApplyInitialTheme(ThemeMode mode)
    {
        var requestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
        if (_rootLayout.RequestedTheme != requestedTheme)
        {
            _rootLayout.RequestedTheme = requestedTheme;
        }
    }

    public void ApplyTheme(ThemeMode mode)
    {
        ApplyInitialTheme(mode);

        RebuildSummaryCards();
        _panelCards.Clear();
        RefreshDashboardPanels();

        if (_controlDeckEditableActive)
        {
            RenderEditableControlDeck();
        }
        else
        {
            RenderControlDeckSummary();
        }

        TryLoadBrandImage();
    }

    private void BuildSummaryCards()
    {
        if (_summaryCardsBound)
        {
            return;
        }

        StartupTrace.Write("BuildSummaryCards // polished-v19");
        _summaryHost.Children.Clear();
        _summaryHost.RowDefinitions.Clear();
        _summaryHost.ColumnDefinitions.Clear();
        var summaryColumns = DetermineSummaryColumns();
        _lastSummaryColumnCount = summaryColumns;
        for (var column = 0; column < summaryColumns; column++)
        {
            _summaryHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        foreach (var card in _viewModel.SummaryCards)
        {
            var badgeHost = IconFactory.CreateTile(card.Title, card.AccentBrush, 48, 16);
            badgeHost.VerticalAlignment = VerticalAlignment.Center;

            var titleText = CreateMutedText(string.Empty, 10);
            titleText.CharacterSpacing = 80;
            titleText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Title)) });

            var valueText = CreatePrimaryText(string.Empty, 27, true);
            valueText.FontFamily = new FontFamily("Segoe UI Variable Display");
            valueText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Value)) });

            var captionText = CreateSecondaryText(string.Empty, 12);
            captionText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Caption)) });

            var details = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    titleText,
                    valueText,
                    captionText,
                },
            };

            var contentGrid = new Grid
            {
                ColumnSpacing = 11,
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.Children.Add(badgeHost);
            contentGrid.Children.Add(details);
            Grid.SetColumn(details, 1);

            var summaryCard = new Border
            {
                DataContext = card,
                Background = CreateSurfaceGradient("#0F1C2D", "#15283F"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(16, 13, 16, 13),
                MinHeight = 98,
                Child = contentGrid,
            };
            _summaryHost.Children.Add(summaryCard);
            var index = _summaryHost.Children.Count - 1;
            while (_summaryHost.RowDefinitions.Count <= index / summaryColumns)
            {
                _summaryHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            Grid.SetColumn(summaryCard, index % summaryColumns);
            Grid.SetRow(summaryCard, index / summaryColumns);
        }

        _summaryCardsBound = true;
    }

    private void RebuildSummaryCards()
    {
        _summaryCardsBound = false;
        _summaryHost.Children.Clear();
        _summaryHost.RowDefinitions.Clear();
        _summaryHost.ColumnDefinitions.Clear();
        BuildSummaryCards();
    }

    private void RefreshPanelToggles()
    {
    }

    private void RefreshDashboardPanels()
    {
        StartupTrace.Write("RefreshDashboardPanels // polished-v19");
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
        _lastDashboardColumnCount = columns;
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
        return width >= 1260 ? 3 : width >= 900 ? 2 : 1;
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        StartupTrace.Write("OnRootLoaded start // polished-v19");

        try
        {
            var config = _viewModel.BuildConfig();
            StartupTrace.Write("OnRootLoaded resumed after first paint // polished-v19");
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await TryLoadHistoryAsync(config);
            await EnsureCollectorRunningAsync(config);
            App.CurrentApp.MarkStartupSettled();
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                TryApplyWindowIcon();
            });
            StartupTrace.Write("OnRootLoaded complete // polished-v19");
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
        Activated -= OnWindowActivated;

        if (_windowIconHandle != 0)
        {
            NativeWindowMethods.DestroyIcon(_windowIconHandle);
            _windowIconHandle = 0;
        }

        if (_collectorService is not null)
        {
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
            _collectorService.CollectionFailed -= OnCollectionFailed;
            await _collectorService.DisposeAsync();
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_windowIconApplied)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(TryApplyWindowIcon);
    }

    private async void OnSaveSettingsClick(object? sender, EventArgs e)
    {
        try
        {
            if (!TryNormalizeScrapeInput(_viewModel.ScrapeIntervalInput, out var normalizedScrapeInput))
            {
                _activeDeckEditor = DeckEditorMode.Scrape;
                _viewModel.StatusText = "Scrape interval must be a whole number of seconds between 1 and 60.";
                UpdateStatusText();
                RenderEditableControlDeck();
                return;
            }

            if (!MainViewModel.TryParseRetentionInput(_viewModel.RetentionHoursInput, out _, out var normalizedRetentionInput))
            {
                _activeDeckEditor = DeckEditorMode.Retention;
                _viewModel.StatusText = "Retention must use m, h, or d. Try 30m, 24h, or 7d.";
                UpdateStatusText();
                RenderEditableControlDeck();
                return;
            }

            _viewModel.ScrapeIntervalInput = normalizedScrapeInput;
            _viewModel.RetentionHoursInput = normalizedRetentionInput;
            _viewModel.StorageDirectory = _viewModel.StorageDirectory.Trim();

            _viewModel.StatusText = "Applying settings";
            UpdateStatusText();

            _viewModel.ApplyPanelVisibility();
            var config = _viewModel.BuildConfig();
            _viewModel.ApplyConfig(config);
            RenderEditableControlDeck();
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

    private void OnThemeQuickToggle(object? sender, EventArgs e)
    {
        _viewModel.SelectedTheme = _viewModel.SelectedTheme == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;

        App.CurrentApp.ApplyTheme(_viewModel.SelectedTheme);
    }

    private void OnToggleGlobalRangeEditor(object? sender, EventArgs e)
    {
        if (_globalRangeEditorVisible)
        {
            HideGlobalRangeEditor();
            return;
        }

        RenderGlobalRangeEditor();
    }

    private void OnGlobalWindowRangeClick(object? sender, EventArgs e)
    {
        if (sender is ActionChip { Tag: int minutes })
        {
            ApplyGlobalWindowRange(minutes);
        }
    }

    private void OnResetAllZoomClick(object? sender, EventArgs e)
    {
        _globalAbsoluteStartUtc = null;
        _globalAbsoluteEndUtc = null;
        foreach (var panel in _viewModel.DashboardPanels)
        {
            panel.ResetZoom();
        }

        HideGlobalRangeEditor();
        RefreshGlobalRangeControls();
    }

    private void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!_hasReceivedFirstSnapshot)
            {
                _hasReceivedFirstSnapshot = true;
                StartupTrace.Write($"First snapshot collected // samples={snapshot.Samples.Count}");
            }

            _viewModel.ApplySnapshot(snapshot);
            if (_globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue)
            {
                foreach (var panel in _viewModel.DashboardPanels.Where(panel => !panel.IsZoomed))
                {
                    panel.ZoomToWindow(_globalAbsoluteStartUtc.Value, _globalAbsoluteEndUtc.Value);
                }
            }

            UpdateStatusText();
        });
    }

    private void OnCollectionFailed(object? sender, Exception ex)
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            StartupTrace.WriteException("Collection failed", ex);
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

            StartupTrace.Write("Queued dashboard refresh after resize // polished-v19");
            RefreshDashboardPanels();
        });
    }

    private void OnRootLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        var nextColumnCount = DetermineDashboardColumns();
        if (nextColumnCount == _lastDashboardColumnCount)
        {
            if (_summaryCardsBound && DetermineSummaryColumns() != _lastSummaryColumnCount)
            {
                BuildSummaryCards();
            }

            return;
        }

        QueueDashboardRefresh();
    }

    private void OnDashboardPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_initialized)
        {
            QueueDashboardRefresh();
            return;
        }

        RefreshDashboardPanels();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.StatusText), StringComparison.Ordinal))
        {
            UpdateStatusText();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.SelectedWindowMinutes), StringComparison.Ordinal))
        {
            RefreshGlobalRangeControls();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.StorageDirectory), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.ScrapeIntervalInput), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.RetentionHoursInput), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.LaunchOnStartup), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.MinimizeToTray), StringComparison.Ordinal))
        {
            if (!_controlDeckEditableActive)
            {
                RenderControlDeckSummary();
            }
        }
    }

    private void OnPanelTogglesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
    }

    private void OnScrapeFieldClick(object? sender, EventArgs e)
    {
        _activeDeckEditor = DeckEditorMode.Scrape;
        RenderEditableControlDeck();
    }

    private void OnRetentionFieldClick(object? sender, EventArgs e)
    {
        _activeDeckEditor = DeckEditorMode.Retention;
        RenderEditableControlDeck();
    }

    private void OnStorageFieldClick(object? sender, EventArgs e)
    {
        _activeDeckEditor = DeckEditorMode.Storage;
        RenderEditableControlDeck();
    }

    private void OnRetentionInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _viewModel.RetentionHoursInput = entry.Text;
        }
    }

    private void OnScrapeInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _viewModel.ScrapeIntervalInput = entry.Text;
        }
    }

    private void OnStorageInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _viewModel.StorageDirectory = entry.Text;
        }
    }

    private void StepScrapeInterval(int direction)
    {
        var next = MoveThroughPresets(_viewModel.EffectiveScrapeIntervalSeconds, ScrapeIntervalPresets, direction);
        _viewModel.ScrapeIntervalInput = next == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : next.ToString(CultureInfo.InvariantCulture);
        RenderEditableControlDeck();
    }

    private void SetScrapeInterval(int seconds)
    {
        _viewModel.ScrapeIntervalInput = seconds == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : seconds.ToString(CultureInfo.InvariantCulture);
        RenderEditableControlDeck();
    }

    private void NudgeScrapeInterval(int delta)
    {
        var next = Math.Clamp(_viewModel.EffectiveScrapeIntervalSeconds + delta, 1, 60);
        SetScrapeInterval(next);
    }

    private void ResetScrapeInterval()
    {
        _viewModel.ScrapeIntervalInput = string.Empty;
        RenderEditableControlDeck();
    }

    private void SetRetentionInput(string value)
    {
        _viewModel.RetentionHoursInput = value;
        RenderEditableControlDeck();
    }

    private void StepRetentionHours(int direction)
    {
        var next = MoveThroughPresets(_viewModel.EffectiveRetentionHours, RetentionHourPresets, direction);
        _viewModel.RetentionHoursInput = next == VaktrConfig.DefaultMaxRetentionHours
            ? string.Empty
            : next.ToString(CultureInfo.InvariantCulture);
        RenderEditableControlDeck();
    }

    private void SetRetentionHours(int hours)
    {
        _viewModel.RetentionHoursInput = hours == VaktrConfig.DefaultMaxRetentionHours
            ? string.Empty
            : hours.ToString(CultureInfo.InvariantCulture);
        RenderEditableControlDeck();
    }

    private void NudgeRetentionHours(int delta)
    {
        var next = Math.Clamp(_viewModel.EffectiveRetentionHours + delta, 1, 24 * 3650);
        SetRetentionHours(next);
    }

    private void ResetRetentionHours()
    {
        _viewModel.RetentionHoursInput = string.Empty;
        RenderEditableControlDeck();
    }

    private void ResetStorageDirectory()
    {
        _viewModel.StorageDirectory = string.Empty;
        RenderEditableControlDeck();
    }

    private void SetStorageDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _viewModel.StorageDirectory = value.Trim();
        RenderEditableControlDeck();
    }

    private static bool TryNormalizeScrapeInput(string? text, out string normalizedText)
    {
        normalizedText = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        if (seconds is < 1 or > 60)
        {
            return false;
        }

        normalizedText = seconds == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : seconds.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private async void OnBrowseStorageClick(object? sender, EventArgs e)
    {
        try
        {
            _activeDeckEditor = DeckEditorMode.Storage;
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            SetStorageDirectory(folder.Path);
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("BrowseStorageClick", ex);
            _viewModel.StatusText = $"Storage picker issue: {ex.Message}";
            UpdateStatusText();
        }
    }

    private static int MoveThroughPresets(int currentValue, IReadOnlyList<int> presets, int direction)
    {
        if (presets.Count == 0)
        {
            return currentValue;
        }

        if (direction > 0)
        {
            foreach (var preset in presets)
            {
                if (preset > currentValue)
                {
                    return preset;
                }
            }

            return presets[^1];
        }

        for (var index = presets.Count - 1; index >= 0; index--)
        {
            if (presets[index] < currentValue)
            {
                return presets[index];
            }
        }

        return presets[0];
    }

    private void TryApplyWindowIcon()
    {
        if (_windowIconApplied)
        {
            return;
        }

        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Vaktr.ico");
            if (File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
                var windowHandle = WindowNative.GetWindowHandle(this);
                if (windowHandle != 0)
                {
                    if (_windowIconHandle != 0)
                    {
                        NativeWindowMethods.DestroyIcon(_windowIconHandle);
                        _windowIconHandle = 0;
                    }

                    _windowIconHandle = NativeWindowMethods.LoadWindowIcon(iconPath);
                    if (_windowIconHandle != 0)
                    {
                        NativeWindowMethods.ApplyWindowIcon(windowHandle, _windowIconHandle);
                        _windowIconApplied = true;
                        StartupTrace.Write("Window icon applied // polished-v19");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("ApplyWindowIcon", ex);
        }
    }

    private void TryLoadBrandImage()
    {
        try
        {
            var imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "vaktr.png");
            if (!File.Exists(imagePath))
            {
                return;
            }

            StartupTrace.Write("TryLoadBrandImage start // polished-v19");
            var bitmap = new BitmapImage();
            bitmap.UriSource = new Uri(imagePath);

            _brandHost.Width = 142;
            _brandHost.Height = 142;
            _brandHost.CornerRadius = new CornerRadius(0);
            _brandHost.Background = null;
            _brandHost.BorderThickness = new Thickness(0);
            _brandHost.Padding = new Thickness(0);
            _brandHost.Child = new Microsoft.UI.Xaml.Controls.Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                Source = bitmap,
            };
            StartupTrace.Write("TryLoadBrandImage complete // polished-v19");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("TryLoadBrandImage", ex);
        }
    }

    private void RenderSummaryPlaceholders()
    {
        _summaryCardsBound = false;
        _summaryHost.Children.Clear();
        _summaryHost.RowDefinitions.Clear();
        _summaryHost.ColumnDefinitions.Clear();
        var summaryColumns = DetermineSummaryColumns();
        _lastSummaryColumnCount = summaryColumns;
        for (var column = 0; column < summaryColumns; column++)
        {
            _summaryHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var placeholders = new[]
        {
            CreatePlaceholderCard("CPU", "Waiting for first sample"),
            CreatePlaceholderCard("Memory", "Loading local history"),
            CreatePlaceholderCard("Disk", "Preparing local boards"),
            CreatePlaceholderCard("Network", "Warming link metrics"),
        };

        for (var index = 0; index < placeholders.Length; index++)
        {
            while (_summaryHost.RowDefinitions.Count <= index / summaryColumns)
            {
                _summaryHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            _summaryHost.Children.Add(placeholders[index]);
            Grid.SetColumn(placeholders[index], index % summaryColumns);
            Grid.SetRow(placeholders[index], index / summaryColumns);
        }
    }

    private int DetermineSummaryColumns()
    {
        var width = _rootLayout.ActualWidth > 0 ? _rootLayout.ActualWidth : 1280;
        return width >= 1120 ? 4 : width >= 820 ? 2 : 1;
    }

    private async Task TryLoadHistoryAsync(VaktrConfig config)
    {
        try
        {
            StartupTrace.Write("TryLoadHistoryAsync start");
            _viewModel.StatusText = "Loading local history";
            UpdateStatusText();

            await _metricStore.InitializeAsync(config, CancellationToken.None);

            var historyWindow = TimeSpan.FromMinutes(Math.Max(config.GraphWindowMinutes, 5));
            var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.Subtract(historyWindow), CancellationToken.None);
            _viewModel.LoadHistory(history);
            RebuildSummaryCards();
            RefreshDashboardPanels();
            StartupTrace.Write($"TryLoadHistoryAsync complete // panels={history.Count}");
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
        StartupTrace.Write("EnsureCollectorRunningAsync start");
        _collectorService ??= new CollectorService(new WindowsMetricCollector(), _metricStore);
        _collectorService.SnapshotCollected -= OnSnapshotCollected;
        _collectorService.CollectionFailed -= OnCollectionFailed;
        _collectorService.SnapshotCollected += OnSnapshotCollected;
        _collectorService.CollectionFailed += OnCollectionFailed;

        _viewModel.StatusText = "Starting telemetry";
        UpdateStatusText();
        await _collectorService.StartAsync(config, CancellationToken.None);

        if (!_summaryCardsBound)
        {
            BuildSummaryCards();
        }
        _viewModel.StatusText = _viewModel.DashboardPanels.Count > 0 ? "Streaming local telemetry" : "Waiting for first sample";
        UpdateStatusText();
        StartupTrace.Write($"EnsureCollectorRunningAsync complete // dashboardPanels={_viewModel.DashboardPanels.Count}");
    }

    private void UpdateStatusText()
    {
        var text = _viewModel.StatusText;
        _statusText.Text = text;

        if (!string.Equals(_lastRenderedStatusText, text, StringComparison.Ordinal))
        {
            _lastRenderedStatusText = text;
            StartupTrace.Write($"StatusText -> {text}");
        }
    }

    private void ApplyGlobalWindowRange(int minutes)
    {
        _globalAbsoluteStartUtc = null;
        _globalAbsoluteEndUtc = null;
        _viewModel.SelectedWindowMinutes = minutes;
        HideGlobalRangeEditor();
        RefreshGlobalRangeControls();
    }

    private void RefreshGlobalRangeControls()
    {
        ApplyGlobalRangeState(_globalFiveMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 5);
        ApplyGlobalRangeState(_globalThirtyMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 30);
        ApplyGlobalRangeState(_globalOneHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 60);
        ApplyGlobalRangeState(_globalTwelveHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 720);
        ApplyGlobalRangeState(_globalTwentyFourHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 1440);
        ApplyGlobalRangeState(_globalSevenDayButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 10080);
        ApplyGlobalRangeState(_globalThirtyDayButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 43200);

        _globalRangeButton.Text = _globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue
            ? "Custom range v"
            : $"{FormatGlobalRangeLabel(_viewModel.SelectedWindowMinutes)} v";
        _globalRangeButton.IsActive = _globalRangeEditorVisible;
        _globalRangeButton.IsFilled = true;
        _globalResetZoomButton.Opacity = _viewModel.DashboardPanels.Any(panel => panel.IsZoomed) ? 1d : 0.82d;
    }

    private void RenderGlobalRangeEditor()
    {
        _globalRangeEditorVisible = true;
        var startText = (_globalAbsoluteStartUtc ?? DateTimeOffset.Now.AddMinutes(-_viewModel.SelectedWindowMinutes)).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var endText = (_globalAbsoluteEndUtc ?? DateTimeOffset.Now).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var currentLabel = _globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue
            ? "Custom absolute range"
            : $"Last {FormatGlobalRangeLabel(_viewModel.SelectedWindowMinutes)}";

        var fromEntry = CreateDeckInlineEntry(startText, "2026-04-02 09:00");
        var toEntry = CreateDeckInlineEntry(endText, "2026-04-02 09:30");
        var quickRangeRowPrimary = CreateChipWrapRow(
            _globalFiveMinuteButton,
            _globalThirtyMinuteButton,
            _globalOneHourButton,
            _globalTwelveHourButton);
        var quickRangeRowSecondary = CreateChipWrapRow(
            _globalTwentyFourHourButton,
            _globalSevenDayButton,
            _globalThirtyDayButton);

        var quickRangeCard = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#132235"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 12, 14, 12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    CreateSectionHeader("LIVE WINDOW", "Keep the board synced to the latest samples with one shared replay range."),
                    quickRangeRowPrimary,
                    quickRangeRowSecondary,
                },
            },
        };

        var fromColumn = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateFieldLabel("From"),
                fromEntry,
            },
        };
        var toColumn = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateFieldLabel("To"),
                toEntry,
            },
        };
        var absoluteGrid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        absoluteGrid.Children.Add(fromColumn);
        absoluteGrid.Children.Add(toColumn);
        Grid.SetColumn(toColumn, 1);

        var absoluteRangeCard = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#132235"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 12, 14, 12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    CreateSectionHeader("ABSOLUTE RANGE", "Pin every panel to an exact local date and time window."),
                    absoluteGrid,
                    CreateSecondaryText("Use local time like 2026-04-02 21:30. This locks the board to a precise slice instead of following the latest sample.", 12),
                },
            },
        };

        var actionButtons = CreateChipRow(
            CreateActionChip("Close", (_, _) => HideGlobalRangeEditor()),
            CreateActionChip("Apply range", (_, _) => ApplyAbsoluteGlobalRange(fromEntry.Text, toEntry.Text), true));
        var actionGrid = new Grid
        {
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        actionGrid.Children.Add(CreateMutedText("Board-level range control with quick presets plus absolute From/To.", 12));
        actionGrid.Children.Add(actionButtons);
        Grid.SetColumn(actionButtons, 1);

        var shell = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Background = ResolveBrush("SurfaceBrush", "#0C1726"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16, 14, 16, 14),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    CreateSectionHeader("TIME RANGE", "Use one board-wide window, or pin every panel to a specific absolute slice."),
                    CreateTopStatusPill(new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            CreateMutedText("CURRENT", 10),
                            CreatePrimaryText(currentLabel, 13, true),
                        },
                    }),
                    quickRangeCard,
                    absoluteRangeCard,
                    actionGrid,
                },
            },
        };

        _globalRangeEditorHost.Child = shell;
        _globalRangeEditorHost.Visibility = Visibility.Visible;
        RefreshGlobalRangeControls();
    }

    private void HideGlobalRangeEditor()
    {
        _globalRangeEditorVisible = false;
        _globalRangeEditorHost.Child = null;
        _globalRangeEditorHost.Visibility = Visibility.Collapsed;
        RefreshGlobalRangeControls();
    }

    private void ApplyAbsoluteGlobalRange(string fromText, string toText)
    {
        if (!TryParseGlobalDateTime(fromText, out var start) || !TryParseGlobalDateTime(toText, out var end) || end <= start)
        {
            _viewModel.StatusText = "Time range must use local date/time like 2026-04-02 21:30, and End must be after From.";
            UpdateStatusText();
            return;
        }

        _globalAbsoluteStartUtc = start;
        _globalAbsoluteEndUtc = end;
        foreach (var panel in _viewModel.DashboardPanels)
        {
            panel.ZoomToWindow(start, end);
        }

        HideGlobalRangeEditor();
        _viewModel.StatusText = $"Viewing {start.LocalDateTime:g} to {end.LocalDateTime:g}";
        UpdateStatusText();
    }

    private string GetGlobalRangeButtonText()
    {
        if (_globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue)
        {
            return $"{_globalAbsoluteStartUtc.Value.LocalDateTime:MM-dd HH:mm} -> {_globalAbsoluteEndUtc.Value.LocalDateTime:MM-dd HH:mm} ▾";
        }

        return $"{FormatGlobalRangeLabel(_viewModel.SelectedWindowMinutes)} ▾";
    }

    private static string FormatGlobalRangeLabel(int minutes) => minutes switch
    {
        <= 5 => "5m",
        <= 30 => "30m",
        <= 60 => "1h",
        <= 720 => "12h",
        <= 1440 => "24h",
        <= 10080 => "7d",
        _ => "30d",
    };

    private static bool TryParseGlobalDateTime(string text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateTime.TryParseExact(
                text.Trim(),
                ["yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var exact))
        {
            value = new DateTimeOffset(exact);
            return true;
        }

        if (DateTime.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            value = new DateTimeOffset(parsed);
            return true;
        }

        return false;
    }

    private static void ApplyGlobalRangeState(ActionChip chip, bool isActive)
    {
        chip.IsActive = isActive;
        chip.IsFilled = isActive;
        chip.Opacity = isActive ? 1d : 0.82d;
    }
}

internal enum DeckEditorMode
{
    Scrape,
    Retention,
    Storage,
}

internal static class NativeWindowMethods
{
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x0010;
    private const uint DefaultSize = 0x0040;
    private const uint WindowSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;

    public static nint LoadWindowIcon(string iconPath) =>
        LoadImageW(0, iconPath, ImageIcon, 0, 0, LoadFromFile | DefaultSize);

    public static void ApplyWindowIcon(nint windowHandle, nint iconHandle)
    {
        SendMessageW(windowHandle, WindowSetIcon, (nint)IconSmall, iconHandle);
        SendMessageW(windowHandle, WindowSetIcon, (nint)IconBig, iconHandle);
    }

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImageW(
        nint instanceHandle,
        string name,
        uint imageType,
        int desiredWidth,
        int desiredHeight,
        uint loadFlags);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(
        nint windowHandle,
        uint message,
        nint wordParameter,
        nint longParameter);

    [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(nint iconHandle);
}
