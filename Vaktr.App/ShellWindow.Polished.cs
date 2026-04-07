using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.UI;
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
    private static BitmapImage? s_brandBitmap;
    private static string? s_brandBitmapPath;
    private DeckEditorMode _activeDeckEditor = DeckEditorMode.Scrape;

    private readonly MainViewModel _viewModel;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly Dictionary<string, TelemetryPanelCard> _panelCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SummaryCardVisual> _summaryCardVisuals = [];

    private Grid _rootLayout;
    private readonly Grid _titleBarDragHost;
    private readonly ScrollViewer _scrollHost;
    private Border? _loadingOverlay;
    private readonly Border _controlsBodyHost;
    private readonly Grid _brandHost;
    private readonly Grid _summaryHost;
    private readonly Grid _dashboardGrid;
    private readonly TextBlock _statusText;
    private readonly ActionChip _globalRangeButton;
    private readonly Border _globalRangeEditorHost;
    private readonly ActionChip _globalOneMinuteButton;
    private readonly ActionChip _globalFiveMinuteButton;
    private readonly ActionChip _globalFifteenMinuteButton;
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
    private bool _snapshotApplyQueued;
    private bool _controlDeckRenderQueued;
    private bool _panelRenderingSuspended;
    private string _lastRenderedStatusText = string.Empty;
    private string _draftScrapeIntervalInput = string.Empty;
    private string _draftRetentionInput = string.Empty;
    private string _draftStorageDirectory = string.Empty;
    private ThemeMode _draftThemeMode;
    private DateTimeOffset? _globalAbsoluteStartUtc;
    private DateTimeOffset? _globalAbsoluteEndUtc;
    private MetricSnapshot? _pendingSnapshot;

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
            ColumnSpacing = 16,
            RowSpacing = 16,
        };
        _dashboardGrid = new Grid
        {
            ColumnSpacing = 20,
            RowSpacing = 20,
            ChildrenTransitions = [new RepositionThemeTransition()],
        };
        _globalRangeButton = CreateActionChip("15m", OnToggleGlobalRangeEditor, true);
        _globalRangeButton.MinWidth = 132;
        _globalRangeEditorHost = new Border
        {
            Visibility = Visibility.Collapsed,
        };
        _globalOneMinuteButton = CreateGlobalRangeChip("1m", 1);
        _globalFiveMinuteButton = CreateGlobalRangeChip("5m", 5);
        _globalFifteenMinuteButton = CreateGlobalRangeChip("15m", 15);
        _globalThirtyMinuteButton = CreateGlobalRangeChip("30m", 30);
        _globalOneHourButton = CreateGlobalRangeChip("1h", 60);
        _globalTwelveHourButton = CreateGlobalRangeChip("12h", 720);
        _globalTwentyFourHourButton = CreateGlobalRangeChip("24h", 1440);
        _globalSevenDayButton = CreateGlobalRangeChip("7d", 10080);
        _globalThirtyDayButton = CreateGlobalRangeChip("30d", 43200);
        _globalResetZoomButton = CreateActionChip("Reset zoom", OnResetAllZoomClick);
        _titleBarDragHost = new Grid
        {
            Height = 34,
            Background = ResolveBrush("AppBackdropBrush", "#030812"),
        };
        _scrollHost = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            ZoomMode = ZoomMode.Disabled,
            IsTabStop = false,
        };
        _scrollHost.ViewChanged += OnScrollHostViewChanged;

        // Show loading screen immediately — lightweight content renders on first frame
        _rootLayout = BuildLoadingScreen();
        Content = _rootLayout;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_titleBarDragHost);

        Closed += OnWindowClosed;
        Activated += OnWindowActivated;

        ApplyInitialTheme(_viewModel.SelectedTheme);
        StartupTrace.Write("ShellWindow ctor complete // polished-v19");
    }

    private bool _fullUiBuilt;

    private void BuildFullUi()
    {
        if (_fullUiBuilt)
        {
            return;
        }

        _fullUiBuilt = true;
        StartupTrace.Write("BuildFullUi start");

        var fullLayout = BuildRootLayout();

        // Transfer loading overlay to the full layout
        if (_loadingOverlay is not null)
        {
            if (_loadingOverlay.Parent is Grid oldParent)
            {
                oldParent.Children.Remove(_loadingOverlay);
            }

            fullLayout.Children.Add(_loadingOverlay);
            Grid.SetRow(_loadingOverlay, 1);
            Canvas.SetZIndex(_loadingOverlay, 100);
        }

        _rootLayout = fullLayout;
        Content = _rootLayout;
        SetTitleBar(_titleBarDragHost);
        ApplyInitialTheme(_viewModel.SelectedTheme);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged += OnPanelTogglesChanged;

        try
        {
            TryApplyWindowIcon();
            TryLoadBrandImage();
            SyncControlDeckDraftsFromViewModel();
            RenderControlDeckSummary();
            BuildSummaryCards();
            RefreshDashboardPanels();
            RefreshGlobalRangeControls();
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("BuildFullUi", ex);
        }

        StartupTrace.Write("BuildFullUi complete");
    }

    public void ApplyInitialTheme(ThemeMode mode)
    {
        var requestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
        if (_rootLayout.RequestedTheme != requestedTheme)
        {
            _rootLayout.RequestedTheme = requestedTheme;
        }

        RefreshWindowChrome();
    }

    public void ApplyTheme(ThemeMode mode)
    {
        var requestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
        ApplyInitialTheme(mode);
        _draftThemeMode = mode;
        RefreshThemeVisuals();
    }

    public void PreviewTheme(ThemeMode mode)
    {
        ApplyInitialTheme(mode);
        _draftThemeMode = mode;
        RefreshThemeVisuals();
    }

    private void RefreshThemeVisuals()
    {
        RunWithDeferredPanelRendering(() =>
        {
            if (_controlDeckEditableActive)
            {
                RenderEditableControlDeck();
            }
            else
            {
                RenderControlDeckSummary();
            }

            if (_globalRangeEditorVisible)
            {
                RenderGlobalRangeEditor();
            }

            RefreshSummaryCardThemeResources();
            foreach (var card in _panelCards.Values)
            {
                card.RefreshThemeResources();
            }

            RefreshGlobalRangeControls();
            foreach (var chip in new[]
            {
                _globalRangeButton, _globalOneMinuteButton, _globalFiveMinuteButton,
                _globalFifteenMinuteButton, _globalThirtyMinuteButton, _globalOneHourButton,
                _globalTwelveHourButton, _globalTwentyFourHourButton, _globalSevenDayButton,
                _globalThirtyDayButton, _globalResetZoomButton,
            })
            {
                chip.RefreshThemeResources();
            }

            RefreshWindowChrome();
        });
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
        _summaryCardVisuals.Clear();
        var summaryColumns = DetermineSummaryColumns();
        _lastSummaryColumnCount = summaryColumns;
        for (var column = 0; column < summaryColumns; column++)
        {
            _summaryHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        foreach (var card in _viewModel.SummaryCards)
        {
            var badgeHost = (Border)IconFactory.CreateTile(card.Title, card.AccentBrush, 48, 16);
            badgeHost.VerticalAlignment = VerticalAlignment.Center;

            var titleText = CreateMutedText(string.Empty, 9.5);
            titleText.CharacterSpacing = 90;
            titleText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Title)) });

            var valueText = CreatePrimaryText(string.Empty, 25, true);
            valueText.FontFamily = new FontFamily("Segoe UI Variable Display");
            valueText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Value)) });

            var captionText = CreateSecondaryText(string.Empty, 11.5);
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
                ColumnSpacing = 12,
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
                Padding = new Thickness(18, 15, 18, 15),
                MinHeight = 100,
                Child = contentGrid,
            };
            _summaryHost.Children.Add(summaryCard);
            _summaryCardVisuals.Add(new SummaryCardVisual(summaryCard, titleText, valueText, captionText, badgeHost, card));
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

    private void RefreshSummaryCardThemeResources()
    {
        if (!_summaryCardsBound)
        {
            return;
        }

        foreach (var visual in _summaryCardVisuals)
        {
            visual.Surface.Background = CreateSurfaceGradient("#0F1C2D", "#15283F");
            visual.Surface.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            visual.TitleText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
            visual.ValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
            visual.CaptionText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
            visual.BadgeHost.Child = IconFactory.CreateTile(visual.ViewModel.Title, visual.ViewModel.AccentBrush, 48, 16);
        }
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
            if (_panelCards.TryGetValue(staleKey, out var staleCard))
            {
                staleCard.RangePresetRequested -= OnPanelRangePresetRequested;
                staleCard.PanelZoomSelectionRequested -= OnPanelZoomSelectionRequested;
                staleCard.PanelZoomResetRequested -= OnPanelZoomResetRequested;
                staleCard.PanelReorderRequested -= OnPanelReorderRequested;
                staleCard.PanelDragEnded -= OnPanelDragEnded;
            }

            _panelCards.Remove(staleKey);
        }

        foreach (var panel in panels)
        {
            if (!_panelCards.TryGetValue(panel.PanelKey, out var card))
            {
                card = new TelemetryPanelCard { Panel = panel };
                card.RangePresetRequested += OnPanelRangePresetRequested;
                card.PanelZoomSelectionRequested += OnPanelZoomSelectionRequested;
                card.PanelZoomResetRequested += OnPanelZoomResetRequested;
                card.PanelReorderRequested += OnPanelReorderRequested;
                card.PanelDragEnded += OnPanelDragEnded;
                _panelCards.Add(panel.PanelKey, card);
            }
            else
            {
                card.Panel = panel;
            }

            card.SetRenderingSuspended(_panelRenderingSuspended);
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

    private void OnRootLoaded(object sender, RoutedEventArgs e)
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
            App.CurrentApp.MarkStartupSettled();
            _ = StartTelemetryAsync(config);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                TryApplyWindowIcon();
            });
            StartupTrace.Write("OnRootLoaded scheduled background startup // polished-v19");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("OnRootLoaded", ex);
            _viewModel.StatusText = $"Startup issue: {ex.Message}";
            UpdateStatusText();
            App.CurrentApp.MarkStartupSettled();
        }
    }

    private async Task StartTelemetryAsync(VaktrConfig config)
    {
        try
        {
            await EnsureCollectorRunningAsync(config);
            _ = TryLoadHistoryAsync(config);
            StartupTrace.Write("Background startup complete // polished-v19");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("StartTelemetryAsync", ex);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                _viewModel.StatusText = $"Startup issue: {ex.Message}";
                UpdateStatusText();
            });
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DashboardPanels.CollectionChanged -= OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged -= OnPanelTogglesChanged;
        Activated -= OnWindowActivated;
        _scrollHost.ViewChanged -= OnScrollHostViewChanged;

        if (_windowIconHandle != 0)
        {
            NativeWindowMethods.DestroyIcon(_windowIconHandle);
            _windowIconHandle = 0;
        }

        if (_collectorService is not null)
        {
            var collectorService = _collectorService;
            _collectorService = null;
            collectorService.SnapshotCollected -= OnSnapshotCollected;
            collectorService.CollectionFailed -= OnCollectionFailed;
            // Fire-and-forget disposal — window closes immediately, cleanup runs in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await collectorService.DisposeAsync();
                }
                catch
                {
                    // Best-effort cleanup — process is exiting
                }
            });
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (!_fullUiBuilt)
        {
            return;
        }

        RefreshWindowChrome();

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
            var previousConfig = _viewModel.BuildConfig();
            if (!TryNormalizeScrapeInput(_draftScrapeIntervalInput, out var normalizedScrapeInput))
            {
                _viewModel.StatusText = "Scrape interval must be a whole number of seconds between 1 and 60.";
                UpdateStatusText();
                RenderEditableControlDeck();
                return;
            }

            if (!MainViewModel.TryParseRetentionInput(_draftRetentionInput, out _, out var normalizedRetentionInput))
            {
                _viewModel.StatusText = "Retention must use m, h, or d. Try 30m, 24h, or 7d.";
                UpdateStatusText();
                RenderEditableControlDeck();
                return;
            }

            _viewModel.ScrapeIntervalInput = normalizedScrapeInput;
            _viewModel.RetentionHoursInput = normalizedRetentionInput;
            _viewModel.StorageDirectory = string.IsNullOrWhiteSpace(_draftStorageDirectory)
                ? string.Empty
                : _draftStorageDirectory.Trim();
            _viewModel.SelectedTheme = _draftThemeMode;

            _viewModel.ApplyPanelVisibility();
            var config = _viewModel.BuildConfig();
            var scrapeChanged = config.ScrapeIntervalSeconds != previousConfig.ScrapeIntervalSeconds;
            var storageChanged = !string.Equals(config.StorageDirectory, previousConfig.StorageDirectory, StringComparison.OrdinalIgnoreCase);
            var previousRetentionWindow = previousConfig.GetRetentionWindow();
            var nextRetentionWindow = config.GetRetentionWindow();
            var retentionChanged =
                nextRetentionWindow != previousRetentionWindow ||
                !string.Equals(config.RetentionInputText, previousConfig.RetentionInputText, StringComparison.OrdinalIgnoreCase);
            var retentionLowered = nextRetentionWindow < previousRetentionWindow;
            var themeChanged = config.Theme != previousConfig.Theme;
            var collectorRestartRequired = scrapeChanged || storageChanged;

            _viewModel.StatusText = collectorRestartRequired
                ? "Applying telemetry settings"
                : retentionLowered
                    ? "Saving settings and pruning older history"
                    : themeChanged
                        ? "Applying theme"
                        : "Saving settings";
            UpdateStatusText();
            _viewModel.ApplyConfig(config);
            SyncControlDeckDraftsFromViewModel();
            _controlDeckEditableActive = false;
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await _configStore.SaveAsync(config, CancellationToken.None);
            if (themeChanged)
            {
                App.CurrentApp.ApplyTheme(config.Theme);
            }
            RenderControlDeckSummary();
            _ = ApplyRuntimeSettingsAsync(config, collectorRestartRequired, retentionChanged, retentionLowered);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Settings issue: {ex.Message}";
            UpdateStatusText();
        }
    }

    private void OnThemeQuickToggle(object? sender, EventArgs e)
    {
        _draftThemeMode = _draftThemeMode == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;
        App.CurrentApp.PreviewTheme(_draftThemeMode);
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
            RunWithDeferredPanelRendering(() => ApplyGlobalWindowRange(minutes));
        }
    }

    private void OnPanelRangePresetRequested(object? sender, TimeRangePresetRequestedEventArgs e)
    {
        RunWithDeferredPanelRendering(() => ApplyGlobalWindowRange((int)e.Preset));
    }

    private void OnPanelZoomSelectionRequested(object? sender, ChartZoomSelectionEventArgs e)
    {
        ApplyAbsoluteGlobalRange(e.StartUtc, e.EndUtc);
    }

    private void OnPanelZoomResetRequested(object? sender, EventArgs e)
    {
        OnResetAllZoomClick(sender, e);
    }

    private void OnPanelDragEnded(object? sender, EventArgs e)
    {
        // Now that the drag is done, place the card at its correct grid position
        // (it was skipped during drag to avoid flicker)
        var panels = _viewModel.DashboardPanels.ToArray();
        var columns = DetermineDashboardColumns();

        for (var index = 0; index < panels.Length; index++)
        {
            if (_panelCards.TryGetValue(panels[index].PanelKey, out var card) && ReferenceEquals(card, sender))
            {
                Grid.SetColumn(card, index % columns);
                Grid.SetRow(card, index / columns);
                break;
            }
        }
    }

    private void OnPanelReorderRequested(object? sender, PanelReorderRequestedEventArgs e)
    {
        if (!_viewModel.MovePanel(e.SourcePanelKey, e.TargetPanelKey))
        {
            return;
        }

        // During drag, do a lightweight position update instead of full grid rebuild
        ReorderDashboardPanelsInPlace();
        DebouncePersistLayout();
    }

    private void ReorderDashboardPanelsInPlace()
    {
        var panels = _viewModel.DashboardPanels.ToArray();
        var columns = DetermineDashboardColumns();

        for (var index = 0; index < panels.Length; index++)
        {
            if (_panelCards.TryGetValue(panels[index].PanelKey, out var card))
            {
                // Skip the actively dragged card — it's positioned by mouse transform,
                // grid changes would fight with the manual positioning and cause flicker
                if (card.IsDragging)
                {
                    continue;
                }

                var targetCol = index % columns;
                var targetRow = index / columns;
                if (Grid.GetColumn(card) != targetCol)
                {
                    Grid.SetColumn(card, targetCol);
                }

                if (Grid.GetRow(card) != targetRow)
                {
                    Grid.SetRow(card, targetRow);
                }
            }
        }
    }

    private bool _persistLayoutQueued;

    private void DebouncePersistLayout()
    {
        if (_persistLayoutQueued)
        {
            return;
        }

        _persistLayoutQueued = true;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _persistLayoutQueued = false;
            _ = PersistLayoutAsync();
        });
    }

    private void OnResetAllZoomClick(object? sender, EventArgs e)
    {
        RunWithDeferredPanelRendering(() =>
        {
            _globalAbsoluteStartUtc = null;
            _globalAbsoluteEndUtc = null;
            _viewModel.ResetGlobalZoom();
        });

        HideGlobalRangeEditor();
        RefreshGlobalRangeControls();
    }

    private void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        _pendingSnapshot = snapshot;
        if (_snapshotApplyQueued)
        {
            return;
        }

        _snapshotApplyQueued = true;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyQueuedSnapshot);
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

    private void ApplyQueuedSnapshot()
    {
        _snapshotApplyQueued = false;
        var snapshot = _pendingSnapshot;
        _pendingSnapshot = null;
        if (snapshot is null)
        {
            return;
        }

        if (!_hasReceivedFirstSnapshot)
        {
            _hasReceivedFirstSnapshot = true;
            StartupTrace.Write($"First snapshot collected // samples={snapshot.Samples.Count}");
            if (_loadingOverlay is not null)
            {
                DismissLoadingOverlay();
            }
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

        if (_pendingSnapshot is not null && !_snapshotApplyQueued)
        {
            _snapshotApplyQueued = true;
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyQueuedSnapshot);
        }
    }

    private void DismissLoadingOverlay()
    {
        if (_loadingOverlay is null)
        {
            return;
        }

        var overlay = _loadingOverlay;
        _loadingOverlay = null;

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, overlay);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Completed += (_, _) =>
        {
            if (overlay.Parent is Grid parentGrid)
            {
                parentGrid.Children.Remove(overlay);
            }
        };
        storyboard.Begin();
    }

    private async Task PersistLayoutAsync()
    {
        try
        {
            await _configStore.SaveAsync(_viewModel.BuildConfig(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("PersistLayout", ex);
        }
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

    private void OnScrollHostViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (e.IsIntermediate)
        {
            SetPanelRenderingSuspended(true);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SetPanelRenderingSuspended(false));
    }

    private void RunWithDeferredPanelRendering(Action action)
    {
        SetPanelRenderingSuspended(true);
        try
        {
            action();
        }
        finally
        {
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SetPanelRenderingSuspended(false));
        }
    }

    private void SetPanelRenderingSuspended(bool suspended)
    {
        if (_panelRenderingSuspended == suspended)
        {
            return;
        }

        _panelRenderingSuspended = suspended;
        foreach (var card in _panelCards.Values)
        {
            card.SetRenderingSuspended(suspended);
        }
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
        if (!_initialized)
        {
            RefreshDashboardPanels();
            return;
        }

        // For move/reorder events, just update grid positions (fast)
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            ReorderDashboardPanelsInPlace();
            return;
        }

        // For add/remove/reset, do a full rebuild
        QueueDashboardRefresh();
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
        BeginControlDeckEdit(DeckEditorMode.Scrape);
    }

    private void OnRetentionFieldClick(object? sender, EventArgs e)
    {
        BeginControlDeckEdit(DeckEditorMode.Retention);
    }

    private void OnStorageFieldClick(object? sender, EventArgs e)
    {
        BeginControlDeckEdit(DeckEditorMode.Storage);
    }

    private void OnRetentionInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _draftRetentionInput = entry.Text;
        }
    }

    private void OnScrapeInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _draftScrapeIntervalInput = entry.Text;
        }
    }

    private void OnStorageInputChanged(object? sender, EventArgs e)
    {
        if (sender is InlineTextEntry entry)
        {
            _draftStorageDirectory = entry.Text;
        }
    }

    private void OnEditSettingsClick(object? sender, EventArgs e)
    {
        BeginControlDeckEdit();
    }

    private void OnCancelSettingsClick(object? sender, EventArgs e)
    {
        var persistedTheme = _viewModel.SelectedTheme;
        var restoreThemePreview = _draftThemeMode != persistedTheme;
        SyncControlDeckDraftsFromViewModel();
        _controlDeckEditableActive = false;
        if (restoreThemePreview)
        {
            App.CurrentApp.ApplyTheme(persistedTheme);
        }
        else
        {
            RenderControlDeckSummary();
        }

        _viewModel.StatusText = "Control deck unchanged";
        UpdateStatusText();
    }

    private void BeginControlDeckEdit(DeckEditorMode mode = DeckEditorMode.Scrape)
    {
        SyncControlDeckDraftsFromViewModel();
        _activeDeckEditor = mode;
        RenderEditableControlDeck();
    }

    private void QueueEditableControlDeckRender()
    {
        if (!_controlDeckEditableActive || _controlDeckRenderQueued)
        {
            return;
        }

        _controlDeckRenderQueued = true;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _controlDeckRenderQueued = false;
            if (_controlDeckEditableActive)
            {
                RenderEditableControlDeck();
            }
        });
    }

    private void SyncControlDeckDraftsFromViewModel()
    {
        _draftScrapeIntervalInput = _viewModel.ScrapeIntervalInput;
        _draftRetentionInput = _viewModel.RetentionHoursInput;
        _draftStorageDirectory = _viewModel.StorageDirectory;
        _draftThemeMode = _viewModel.SelectedTheme;
    }

    private int GetDraftScrapeIntervalSeconds() =>
        int.TryParse(_draftScrapeIntervalInput?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
        seconds is >= 1 and <= 60
            ? seconds
            : VaktrConfig.DefaultScrapeIntervalSeconds;

    private int GetDraftRetentionHours() =>
        MainViewModel.TryParseRetentionInput(_draftRetentionInput, out var hours, out _)
            ? hours
            : VaktrConfig.DefaultMaxRetentionHours;

    private void StepScrapeInterval(int direction)
    {
        var next = MoveThroughPresets(GetDraftScrapeIntervalSeconds(), ScrapeIntervalPresets, direction);
        _draftScrapeIntervalInput = next == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : next.ToString(CultureInfo.InvariantCulture);
        QueueEditableControlDeckRender();
    }

    private void SetScrapeInterval(int seconds)
    {
        _draftScrapeIntervalInput = seconds == VaktrConfig.DefaultScrapeIntervalSeconds
            ? string.Empty
            : seconds.ToString(CultureInfo.InvariantCulture);
        QueueEditableControlDeckRender();
    }

    private void NudgeScrapeInterval(int delta)
    {
        var next = Math.Clamp(_viewModel.EffectiveScrapeIntervalSeconds + delta, 1, 60);
        SetScrapeInterval(next);
    }

    private void ResetScrapeInterval()
    {
        _draftScrapeIntervalInput = string.Empty;
        QueueEditableControlDeckRender();
    }

    private void SetRetentionInput(string value)
    {
        _draftRetentionInput = value;
        QueueEditableControlDeckRender();
    }

    private void StepRetentionHours(int direction)
    {
        var next = MoveThroughPresets(GetDraftRetentionHours(), RetentionHourPresets, direction);
        _draftRetentionInput = FormatRetentionDraftInput(next);
        QueueEditableControlDeckRender();
    }

    private void SetRetentionHours(int hours)
    {
        _draftRetentionInput = FormatRetentionDraftInput(hours);
        QueueEditableControlDeckRender();
    }

    private void NudgeRetentionHours(int delta)
    {
        var next = Math.Clamp(GetDraftRetentionHours() + delta, 1, 24 * 3650);
        SetRetentionHours(next);
    }

    private void ResetRetentionHours()
    {
        _draftRetentionInput = string.Empty;
        QueueEditableControlDeckRender();
    }

    private static string FormatRetentionDraftInput(int hours) =>
        hours <= 0 ? string.Empty : MainViewModel.FormatRetentionInput(hours);

    private void ResetStorageDirectory()
    {
        _draftStorageDirectory = string.Empty;
        QueueEditableControlDeckRender();
    }

    private void SetStorageDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _draftStorageDirectory = value.Trim();
        QueueEditableControlDeckRender();
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

    private async Task ApplyRuntimeSettingsAsync(
        VaktrConfig config,
        bool collectorRestartRequired,
        bool retentionChanged,
        bool retentionLowered)
    {
        try
        {
            await Task.Run(async () =>
            {
                if (collectorRestartRequired)
                {
                    if (_collectorService is null)
                    {
                        _collectorService = new CollectorService(new WindowsMetricCollector(), _metricStore);
                        _collectorService.SnapshotCollected += OnSnapshotCollected;
                        _collectorService.CollectionFailed += OnCollectionFailed;
                    }

                    await _collectorService.StartAsync(config, CancellationToken.None);
                }
                else if (retentionChanged)
                {
                    await _metricStore.InitializeAsync(config, CancellationToken.None);
                    if (retentionLowered)
                    {
                        await _metricStore.PruneAsync(config, CancellationToken.None);
                    }
                }
            });

            _viewModel.StatusText = _viewModel.DashboardPanels.Count > 0
                ? "Streaming local telemetry"
                : "Waiting for first sample";
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Settings issue: {ex.Message}";
            UpdateStatusText();
        }
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
            // Try ico first, then fall back to png
            var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Vaktr.ico");
            var pngPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "vaktr.png");
            var iconPath = File.Exists(icoPath) ? icoPath : File.Exists(pngPath) ? pngPath : null;

            if (iconPath is not null)
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
                    }
                }

                _windowIconApplied = true;
                StartupTrace.Write($"Window icon applied from {System.IO.Path.GetFileName(iconPath)}");
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
            if (s_brandBitmap is null || !string.Equals(s_brandBitmapPath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(imagePath);
                s_brandBitmap = bitmap;
                s_brandBitmapPath = imagePath;
            }

            _brandHost.Width = 96;
            _brandHost.Height = 96;
            _brandHost.Children.Clear();
            _brandHost.Children.Add(new Microsoft.UI.Xaml.Controls.Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                Source = s_brandBitmap,
            });
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
        var cardCount = _viewModel.SummaryCards.Count;
        return width >= 1320 && cardCount >= 5 ? 5
            : width >= 1120 ? 4
            : width >= 820 ? 2
            : 1;
    }

    private async Task TryLoadHistoryAsync(VaktrConfig config)
    {
        try
        {
            StartupTrace.Write("TryLoadHistoryAsync start");
            // Load the full retention window so users can zoom out to see all retained data
            var retentionWindow = config.GetRetentionWindow();
            var historyWindow = retentionWindow;
            var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.Subtract(historyWindow), CancellationToken.None);
            _viewModel.LoadHistory(history);
            StartupTrace.Write($"TryLoadHistoryAsync complete // panels={history.Count}");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("History load", ex);
            if (!_hasReceivedFirstSnapshot)
            {
                _viewModel.StatusText = "History unavailable, starting live telemetry";
                UpdateStatusText();
            }
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
        _viewModel.ApplyGlobalWindowRange(minutes);
        HideGlobalRangeEditor();
        RefreshGlobalRangeControls();
    }

    private void RefreshGlobalRangeControls()
    {
        ApplyGlobalRangeState(_globalOneMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 1);
        ApplyGlobalRangeState(_globalFiveMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 5);
        ApplyGlobalRangeState(_globalFifteenMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 15);
        ApplyGlobalRangeState(_globalThirtyMinuteButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 30);
        ApplyGlobalRangeState(_globalOneHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 60);
        ApplyGlobalRangeState(_globalTwelveHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 720);
        ApplyGlobalRangeState(_globalTwentyFourHourButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 1440);
        ApplyGlobalRangeState(_globalSevenDayButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 10080);
        ApplyGlobalRangeState(_globalThirtyDayButton, !_globalAbsoluteStartUtc.HasValue && _viewModel.SelectedWindowMinutes == 43200);

        _globalRangeButton.Text = BuildGlobalRangeButtonText();
        _globalRangeButton.IsActive = _globalRangeEditorVisible;
        _globalRangeButton.IsFilled = true;
        _globalResetZoomButton.Opacity = _viewModel.DashboardPanels.Any(panel => panel.IsZoomed) ? 1d : 0.82d;
    }

    private void RefreshWindowChrome()
    {
        try
        {
            var titleBar = AppWindow.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            var transparent = Color.FromArgb(0, 0, 0, 0);
            var foreground = App.CurrentApp.ResolveThemeColor("TextSecondaryBrush", _rootLayout.RequestedTheme == ElementTheme.Light ? "#4A6078" : "#C6D7EA");
            var hoverBackground = App.CurrentApp.ResolveThemeColor("SurfaceElevatedBrush", _rootLayout.RequestedTheme == ElementTheme.Light ? "#F4F8FC" : "#112033");
            var pressedBackground = App.CurrentApp.ResolveThemeColor("SurfaceStrongBrush", _rootLayout.RequestedTheme == ElementTheme.Light ? "#EDF4FB" : "#18314A");
            var inactiveForeground = App.CurrentApp.ResolveThemeColor("TextMutedBrush", _rootLayout.RequestedTheme == ElementTheme.Light ? "#6F8399" : "#8098B2");

            titleBar.BackgroundColor = transparent;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveBackgroundColor = transparent;
            titleBar.InactiveForegroundColor = inactiveForeground;
            titleBar.ButtonBackgroundColor = transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveBackgroundColor = transparent;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
            titleBar.ButtonHoverBackgroundColor = hoverBackground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = pressedBackground;
            titleBar.ButtonPressedForegroundColor = foreground;
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("RefreshWindowChrome", ex);
        }
    }

    private void RenderGlobalRangeEditor()
    {
        _globalRangeEditorVisible = true;
        var startText = (_globalAbsoluteStartUtc ?? DateTimeOffset.Now.AddMinutes(-_viewModel.SelectedWindowMinutes)).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var endText = (_globalAbsoluteEndUtc ?? DateTimeOffset.Now).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var currentLabel = _globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue
            ? "Pinned window"
            : $"Last {FormatGlobalRangeLabel(_viewModel.SelectedWindowMinutes)}";
        var currentMode = _globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue
            ? "Absolute"
            : "Rolling";

        var fromEntry = CreateDeckInlineEntry(startText, "2026-04-02 09:00");
        var toEntry = CreateDeckInlineEntry(endText, "2026-04-02 09:30");
        var quickRangeRowPrimary = CreateChipWrapRow(
            _globalOneMinuteButton,
            _globalFiveMinuteButton,
            _globalFifteenMinuteButton,
            _globalThirtyMinuteButton,
            _globalOneHourButton);
        var quickRangeRowSecondary = CreateChipWrapRow(
            _globalTwelveHourButton,
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
                    CreateSectionHeader("QUICK RANGES", "Jump the whole board to a preset replay window."),
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
                    CreateSectionHeader("ABSOLUTE RANGE", "Pin every panel to one precise local time slice."),
                    absoluteGrid,
                    CreateSecondaryText("Use local time like 2026-04-02 21:30.", 12),
                },
            },
        };

        var infoRow = new Grid
        {
            ColumnSpacing = 12,
        };
        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        infoRow.Children.Add(CreateInfoPanel("Current", currentLabel));
        var modePanel = CreateInfoPanel("Mode", currentMode);
        infoRow.Children.Add(modePanel);
        Grid.SetColumn(modePanel, 1);

        var actionButtons = CreateChipRow(
            CreateActionChip("Close", (_, _) => HideGlobalRangeEditor()),
            CreateActionChip("Now", (_, _) =>
            {
                var next = DateTimeOffset.Now.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                toEntry.Text = next;
            }),
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
        actionGrid.Children.Add(CreateMutedText("Applies to every panel.", 12));
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
                    CreateSectionHeader("TIME RANGE", "Keep every panel in sync with one shared timeline."),
                    infoRow,
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

        ApplyAbsoluteGlobalRange(start, end);
    }

    private void ApplyAbsoluteGlobalRange(DateTimeOffset start, DateTimeOffset end)
    {
        RunWithDeferredPanelRendering(() =>
        {
            _globalAbsoluteStartUtc = start;
            _globalAbsoluteEndUtc = end;
            _viewModel.ApplyGlobalAbsoluteRange(start, end);
        });

        HideGlobalRangeEditor();
        _viewModel.StatusText = $"Pinned {start.LocalDateTime:g} to {end.LocalDateTime:g}";
        UpdateStatusText();
    }

    private string BuildGlobalRangeButtonText()
    {
        if (_globalAbsoluteStartUtc.HasValue && _globalAbsoluteEndUtc.HasValue)
        {
            return $"{_globalAbsoluteStartUtc.Value.LocalDateTime:MM-dd HH:mm} -> {_globalAbsoluteEndUtc.Value.LocalDateTime:MM-dd HH:mm}";
        }

        return FormatGlobalRangeLabel(_viewModel.SelectedWindowMinutes);
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
        <= 1 => "1m",
        <= 5 => "5m",
        <= 15 => "15m",
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

internal sealed record SummaryCardVisual(
    Border Surface,
    TextBlock TitleText,
    TextBlock ValueText,
    TextBlock CaptionText,
    Border BadgeHost,
    SummaryCardViewModel ViewModel);

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
