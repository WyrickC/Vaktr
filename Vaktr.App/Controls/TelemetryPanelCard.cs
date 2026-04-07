using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed class TelemetryPanelCard : UserControl
{
    public static readonly DependencyProperty PanelProperty =
        DependencyProperty.Register(
            nameof(Panel),
            typeof(MetricPanelViewModel),
            typeof(TelemetryPanelCard),
            new PropertyMetadata(null, OnPanelChanged));

    private readonly Border _cardBorder;
    private readonly Border _badgeBorder;
    private readonly Border _edgeGlow;
    private readonly Grid _badgeIconHost;
    private readonly TextBlock _footerText;
    private readonly TextBlock _scaleText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _currentValueText;
    private readonly TextBlock _secondaryValueText;
    private readonly TelemetryChart _chart;
    private readonly UsageGauge _gauge;
    private readonly Grid _visualGrid;
    private readonly Border _chartFrame;
    private readonly Border _rangeShell;
    private readonly Border _scalePill;
    private readonly StackPanel _legendHost;
    private readonly ScrollViewer _legendScroller;
    private readonly Border _processSection;
    private readonly TextBlock _processSectionTitleText;
    private readonly TextBlock _processLabelText;
    private readonly TextBlock _activityLabelText;
    private readonly TextBlock _valueLabelText;
    private readonly StackPanel _processRowsHost;
    private readonly ScrollViewer _processScroller;
    private readonly ActionChip _oneMinuteButton;
    private readonly ActionChip _fiveMinuteButton;
    private readonly ActionChip _fifteenMinuteButton;
    private readonly ActionChip _oneHourButton;
    private readonly ActionChip _sortHighestButton;
    private readonly ActionChip _sortLowestButton;
    private readonly ActionChip _sortNameButton;
    private readonly ActionChip _perProcessChartButton;
    private readonly Dictionary<string, (Border Row, TextBlock NameText, TextBlock ValueText, Ellipse Dot)> _legendRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Border Row, TextBlock NameText, TextBlock ValueText, TextBlock CaptionText, Border MeterFill, Border MeterTrack)> _processRowParts = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBlock _processExpandText;
    private bool _processSectionRevealed;
    private MetricPanelViewModel? _observedPanel;
    private bool _refreshQueued;
    private bool _isDropTarget;
    private IReadOnlyList<ChartSeriesViewModel>? _lastRenderedSeries;
    private IReadOnlyList<ProcessListItemViewModel>? _lastRenderedProcessRows;
    private DateTimeOffset _lastRenderedWindowStartUtc;
    private DateTimeOffset _lastRenderedWindowEndUtc;
    private double _lastRenderedCeilingValue = double.NaN;
    private MetricUnit _lastRenderedUnit;
    private TimeRangePreset _lastRenderedRange;
    private ProcessSortMode _lastRenderedSortMode;
    private bool _lastRenderedZoomState;
    private bool _lastRenderedSupportsProcessTable;
    private bool _lastRenderedGaugeMode;
    private bool _isHeaderPointerDown;
    private bool _isHeaderDragActive;
    private FrameworkElement? _headerDragHandle;
    private readonly TranslateTransform _dragTranslate = new();
    private Windows.Foundation.Point _dragMouseStart;
    private TelemetryPanelCard? _activeDropTargetCard;
    private List<(TelemetryPanelCard Card, Windows.Foundation.Rect Bounds)>? _dragTargetCache;
    private long _lastSwapTicks;
    private bool _isEffectivelyVisible = true;
    private bool _isRenderingSuspended;
    private bool _deferredRefreshPending;

    public event EventHandler<TimeRangePresetRequestedEventArgs>? RangePresetRequested;

    public event EventHandler<ChartZoomSelectionEventArgs>? PanelZoomSelectionRequested;

    public event EventHandler? PanelZoomResetRequested;

    public event EventHandler<PanelReorderRequestedEventArgs>? PanelReorderRequested;

    public event EventHandler? PanelDragEnded;

    public bool IsDragging => _isHeaderDragActive;

    public TelemetryPanelCard()
    {
        MinHeight = 388;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        RenderTransform = _dragTranslate;

        _badgeIconHost = new Grid
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _footerText = CreateTextBlock(fontSize: 10);
        _footerText.CharacterSpacing = 30;
        _footerText.FontStyle = Windows.UI.Text.FontStyle.Italic;
        _scaleText = CreateTextBlock("Segoe UI Variable Text", 10.5, FontWeights.Normal);
        _scaleText.TextWrapping = TextWrapping.NoWrap;
        _scaleText.TextTrimming = TextTrimming.CharacterEllipsis;
        _scaleText.MaxWidth = 220;
        _titleText = CreateTextBlock("Segoe UI Variable Display", 17.5, FontWeights.SemiBold);
        _titleText.TextTrimming = TextTrimming.CharacterEllipsis;
        _titleText.TextWrapping = TextWrapping.NoWrap;
        _titleText.MaxLines = 1;
        _currentValueText = CreateTextBlock("Bahnschrift", 22, FontWeights.SemiBold);
        _secondaryValueText = CreateTextBlock(fontSize: 11.5);

        _badgeBorder = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            Background = CreateSurfaceGradient("#102131", "#17304A"),
            Child = _badgeIconHost,
        };

        _edgeGlow = new Border
        {
            Width = 0,
            Height = 184,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-2, 6, 0, 6),
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        _oneMinuteButton = CreateRangeButton("1m", TimeRangePreset.OneMinute);
        _fiveMinuteButton = CreateRangeButton("5m", TimeRangePreset.FiveMinutes);
        _fifteenMinuteButton = CreateRangeButton("15m", TimeRangePreset.FifteenMinutes);
        _oneHourButton = CreateRangeButton("1h", TimeRangePreset.OneHour);

        _visualGrid = new Grid
        {
            ColumnSpacing = 18,
        };
        _visualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _visualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _gauge = new UsageGauge
        {
            Width = 156,
            Height = 156,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _chart = new TelemetryChart
        {
            Height = 188,
            MinHeight = 188,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _chart.ZoomSelectionRequested += OnChartZoomSelectionRequested;
        _chart.ZoomResetRequested += OnChartZoomResetRequested;

        _chartFrame = new Border
        {
            Background = CreateSurfaceGradient("#0E1A2B", "#13263B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(11, 11, 11, 10),
            Child = _chart,
        };

        _visualGrid.Children.Add(_gauge);
        _visualGrid.Children.Add(_chartFrame);
        Grid.SetColumn(_chartFrame, 1);

        _legendHost = new StackPanel
        {
            Spacing = 6,
        };
        _legendScroller = new ScrollViewer
        {
            MaxHeight = 400,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled,
            Content = _legendHost,
        };

        _sortHighestButton = CreateProcessSortButton("Highest", ProcessSortMode.Highest);
        _sortLowestButton = CreateProcessSortButton("Lowest", ProcessSortMode.Lowest);
        _sortNameButton = CreateProcessSortButton("Name", ProcessSortMode.Name);
        _perProcessChartButton = new ActionChip { Text = "Chart", MinHeight = 26, MinWidth = 36 };
        _perProcessChartButton.Click += OnPerProcessChartToggle;

        _processSectionTitleText = CreateTextBlock("Segoe UI Variable Text", 11, FontWeights.Medium);
        _processRowsHost = new StackPanel
        {
            Spacing = 0,
        };
        _processScroller = new ScrollViewer
        {
            MaxHeight = 320,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled,
            Content = _processRowsHost,
        };

        var processHeader = new Grid
        {
            ColumnSpacing = 10,
        };
        processHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        processHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        processHeader.Children.Add(_processSectionTitleText);

        var sortHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children =
            {
                _perProcessChartButton,
                _sortHighestButton,
                _sortLowestButton,
                _sortNameButton,
            },
        };
        processHeader.Children.Add(sortHost);
        Grid.SetColumn(sortHost, 1);

        var processColumns = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(6, 0, 6, 4),
        };
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 90 });
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _processLabelText = CreateTextBlock(fontSize: 9.5);
        _processLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _processLabelText.CharacterSpacing = 60;
        _processLabelText.Text = "PROCESS";
        processColumns.Children.Add(_processLabelText);

        _activityLabelText = CreateTextBlock(fontSize: 9.5);
        _activityLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _activityLabelText.CharacterSpacing = 60;
        _activityLabelText.Text = "DETAILS";
        processColumns.Children.Add(_activityLabelText);
        Grid.SetColumn(_activityLabelText, 1);

        _valueLabelText = CreateTextBlock(fontSize: 9.5);
        _valueLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _valueLabelText.CharacterSpacing = 60;
        _valueLabelText.Text = "VALUE";
        _valueLabelText.HorizontalAlignment = HorizontalAlignment.Right;
        processColumns.Children.Add(_valueLabelText);
        Grid.SetColumn(_valueLabelText, 3);

        _processExpandText = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 10.5,
            Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0, 6, 0, 2),
            Visibility = Visibility.Collapsed,
        };
        var processExpandButton = new Border
        {
            Child = _processExpandText,
            IsHitTestVisible = true,
        };
        processExpandButton.Tapped += OnProcessExpandToggle;

        _processSection = new Border
        {
            Background = CreateSurfaceGradient("#0F1C2D", "#14263A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(13, 12, 13, 12),
            Visibility = Visibility.Collapsed,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    processHeader,
                    processColumns,
                    _processScroller,
                    processExpandButton,
                },
            },
        };

        var rangeHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children =
            {
                _oneMinuteButton,
                _fiveMinuteButton,
                _fifteenMinuteButton,
                _oneHourButton,
            },
        };

        _rangeShell = new Border
        {
            Background = CreateSurfaceGradient("#102031", "#15283E"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(6, 5, 6, 5),
            Child = rangeHost,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var metaGrid = new Grid
        {
            ColumnSpacing = 12,
        };
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        metaGrid.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _badgeBorder,
                _footerText,
            },
        });
        _scalePill = new Border
        {
            Background = ResolveBrush("PanelOverlayBrush", "#112132"),
            BorderBrush = ResolveBrush("SurfaceStrongBrush", "#20364C"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 4, 10, 4),
            Child = _scaleText,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            MinHeight = 28,
        };
        metaGrid.Children.Add(_scalePill);
        Grid.SetColumn(_scalePill, 1);

        var headerContent = new StackPanel
        {
            Spacing = 3,
            Padding = new Thickness(0, 2, 0, 2),
            Background = BrushFactory.CreateBrush("#00FFFFFF"),
            Children =
            {
                metaGrid,
                _titleText,
                _currentValueText,
                _secondaryValueText,
            },
        };
        var headerGrid = new Grid
        {
            ColumnSpacing = 12,
        };
        headerGrid.Background = BrushFactory.CreateBrush("#00FFFFFF");
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ManipulationMode = ManipulationModes.None;
        headerGrid.Children.Add(headerContent);
        headerGrid.Children.Add(_rangeShell);
        Grid.SetColumn(_rangeShell, 1);
        headerGrid.PointerPressed += OnHeaderPointerPressed;
        headerGrid.PointerMoved += OnHeaderPointerMoved;
        headerGrid.PointerReleased += OnHeaderPointerReleased;
        headerGrid.PointerCaptureLost += OnHeaderPointerCaptureLost;
        _headerDragHandle = headerGrid;

        _cardBorder = new Border
        {
            Background = CreateSurfaceGradient("#0E1B2C", "#13253A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(18, 16, 18, 16),
            Child = new Grid
            {
                Children =
                {
                    _edgeGlow,
                    new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            headerGrid,
                            _visualGrid,
                            _legendScroller,
                            _processSection,
                        },
                    },
                },
            },
        };
        _cardBorder.PointerEntered += (_, _) => SetHoverState(true);
        _cardBorder.PointerExited += (_, _) => SetHoverState(false);
        EffectiveViewportChanged += OnEffectiveViewportChanged;

        Content = _cardBorder;
        Opacity = 0;
        var entrancePlayed = false;
        Loaded += (_, _) =>
        {
            RefreshFromPanel();
            if (!entrancePlayed)
            {
                entrancePlayed = true;
                PlayEntranceAnimation();
            }
            else
            {
                Opacity = 1;
            }
        };
    }

    public MetricPanelViewModel? Panel
    {
        get => (MetricPanelViewModel?)GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    private static void OnPanelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (TelemetryPanelCard)dependencyObject;
        control.DetachPanel(args.OldValue as MetricPanelViewModel);
        control.AttachPanel(args.NewValue as MetricPanelViewModel);
        control.RefreshFromPanel();
    }

    private void AttachPanel(MetricPanelViewModel? panel)
    {
        _observedPanel = panel;
        if (panel is not null)
        {
            panel.PropertyChanged += OnPanelPropertyChanged;
        }
    }

    private void DetachPanel(MetricPanelViewModel? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.PropertyChanged -= OnPanelPropertyChanged;
        if (ReferenceEquals(_observedPanel, panel))
        {
            _observedPanel = null;
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRenderingSuspended || (!_isEffectivelyVisible && !IsPriorityPanelProperty(e.PropertyName)))
        {
            _deferredRefreshPending = true;
            return;
        }

        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _refreshQueued = false;
            RefreshFromPanel();
        });
    }

    private void RefreshFromPanel()
    {
        var panel = Panel;
        if (panel is null)
        {
            UpdateBadgeIcon(null);
            _footerText.Text = string.Empty;
            _scaleText.Text = string.Empty;
            _scalePill.Visibility = Visibility.Collapsed;
            _titleText.Text = "Telemetry";
            _currentValueText.Text = "Waiting";
            _secondaryValueText.Text = "Collecting hardware samples";
            _chart.Series = Array.Empty<ChartSeriesViewModel>();
            _chart.CeilingValue = 0d;
            _chart.EmptyStateText = "Waiting for samples";
            _legendHost.Children.Clear();
            _legendRows.Clear();
            _legendScroller.Visibility = Visibility.Collapsed;
            _processRowsHost.Children.Clear();
            _processRowParts.Clear();
            _processSection.Visibility = Visibility.Collapsed;
            RefreshRangeButtons(null);
            RefreshProcessSortButtons(null);
            RefreshVisualMode(null);
            _lastRenderedSeries = null;
            _lastRenderedProcessRows = null;
            _lastRenderedWindowStartUtc = default;
            _lastRenderedWindowEndUtc = default;
            _lastRenderedCeilingValue = double.NaN;
            return;
        }

        UpdateBadgeIcon(panel);
        _footerText.Text = panel.FooterText;
        _titleText.Text = panel.Title;
        _currentValueText.Text = panel.CurrentValue;
        _secondaryValueText.Text = panel.SecondaryValue;
        _scaleText.Text = panel.ScaleLabel;
        _chart.EmptyStateText = panel.EmptyStateText;
        _scalePill.Visibility = string.IsNullOrWhiteSpace(panel.ScaleLabel) ? Visibility.Collapsed : Visibility.Visible;

        if (!_isEffectivelyVisible)
        {
            ApplyPalette(panel);
            return;
        }

        var chartChanged =
            !ReferenceEquals(_lastRenderedSeries, panel.VisibleSeries) ||
            _lastRenderedUnit != panel.Unit ||
            _lastRenderedWindowStartUtc != panel.WindowStartUtc ||
            _lastRenderedWindowEndUtc != panel.WindowEndUtc ||
            !double.Equals(_lastRenderedCeilingValue, panel.ChartCeilingValue);
        if (chartChanged)
        {
            _chart.Series = panel.VisibleSeries;
            _chart.Unit = panel.Unit;
            _chart.WindowStartUtc = panel.WindowStartUtc;
            _chart.WindowEndUtc = panel.WindowEndUtc;
            _chart.CeilingValue = panel.ChartCeilingValue;
            _lastRenderedSeries = panel.VisibleSeries;
            _lastRenderedUnit = panel.Unit;
            _lastRenderedWindowStartUtc = panel.WindowStartUtc;
            _lastRenderedWindowEndUtc = panel.WindowEndUtc;
            _lastRenderedCeilingValue = panel.ChartCeilingValue;
            RefreshLegend(panel);
        }

        _gauge.Value = panel.GaugeValue;
        _gauge.AccentBrush = panel.AccentBrush;
        _gauge.Caption = panel.PrefersGaugeVisual ? "Capacity" : "Live";

        if (!ReferenceEquals(_lastRenderedProcessRows, panel.ProcessRows))
        {
            RefreshProcessRows(panel);
            _lastRenderedProcessRows = panel.ProcessRows;
        }

        if (_lastRenderedRange != panel.SelectedRange || _lastRenderedZoomState != panel.IsZoomed)
        {
            RefreshRangeButtons(panel);
            _lastRenderedRange = panel.SelectedRange;
            _lastRenderedZoomState = panel.IsZoomed;
        }

        if (_lastRenderedSortMode != panel.ProcessSortMode)
        {
            RefreshProcessSortButtons(panel);
            _lastRenderedSortMode = panel.ProcessSortMode;
        }

        if (_lastRenderedSupportsProcessTable != panel.SupportsProcessTable || _lastRenderedGaugeMode != panel.PrefersGaugeVisual)
        {
            RefreshVisualMode(panel);
            _lastRenderedSupportsProcessTable = panel.SupportsProcessTable;
            _lastRenderedGaugeMode = panel.PrefersGaugeVisual;
        }

        ApplyPalette(panel);
    }

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
    {
        var viewport = args.EffectiveViewport;
        var isVisible = viewport.Width > 0 && viewport.Height > 0;
        if (_isEffectivelyVisible == isVisible)
        {
            return;
        }

        _isEffectivelyVisible = isVisible;
        if (_isEffectivelyVisible && _deferredRefreshPending)
        {
            _deferredRefreshPending = false;
            RefreshFromPanel();
        }
    }

    public void SetRenderingSuspended(bool suspended)
    {
        if (_isRenderingSuspended == suspended)
        {
            return;
        }

        _isRenderingSuspended = suspended;
        if (!_isRenderingSuspended && _deferredRefreshPending)
        {
            _deferredRefreshPending = false;
            RefreshFromPanel();
        }
    }

    private static bool IsPriorityPanelProperty(string? propertyName)
    {
        return string.Equals(propertyName, nameof(MetricPanelViewModel.CurrentValue), StringComparison.Ordinal) ||
               string.Equals(propertyName, nameof(MetricPanelViewModel.SecondaryValue), StringComparison.Ordinal) ||
               string.Equals(propertyName, nameof(MetricPanelViewModel.FooterText), StringComparison.Ordinal) ||
               string.Equals(propertyName, nameof(MetricPanelViewModel.ScaleLabel), StringComparison.Ordinal);
    }

    public void RefreshThemeResources()
    {
        _cardBorder.Background = CreateSurfaceGradient("#0E1B2C", "#13253A");
        _cardBorder.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _rangeShell.Background = CreateSurfaceGradient("#102031", "#15283E");
        _rangeShell.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _scalePill.Background = ResolveBrush("PanelOverlayBrush", "#112132");
        _scalePill.BorderBrush = ResolveBrush("SurfaceStrongBrush", "#20364C");
        _chartFrame.Background = CreateSurfaceGradient("#0E1A2B", "#13263B");
        _chartFrame.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _processSection.Background = CreateSurfaceGradient("#0F1C2D", "#14263A");
        _processSection.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _legendScroller.Background = null;
        _processScroller.Background = null;
        _processLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _activityLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _valueLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");

        _oneMinuteButton.RefreshThemeResources();
        _fiveMinuteButton.RefreshThemeResources();
        _fifteenMinuteButton.RefreshThemeResources();
        _oneHourButton.RefreshThemeResources();
        _sortHighestButton.RefreshThemeResources();
        _sortLowestButton.RefreshThemeResources();
        _sortNameButton.RefreshThemeResources();
        _perProcessChartButton.RefreshThemeResources();

        foreach (var rowParts in _legendRows.Values)
        {
            rowParts.Row.Background = CreateSurfaceGradient("#101C2D", "#132438");
            rowParts.Row.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            rowParts.NameText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
            rowParts.ValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        }

        foreach (var rowParts in _processRowParts.Values)
        {
            rowParts.Row.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            rowParts.NameText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
            rowParts.ValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
            rowParts.CaptionText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
            rowParts.MeterTrack.Background = ResolveBrush("SurfaceStrongBrush", "#162A41");
        }

        if (Panel is { } panel)
        {
            UpdateBadgeIcon(panel);
            ApplyPalette(panel);
        }
        else
        {
            _footerText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
            _scaleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
            _titleText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
            _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
            _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
            _processSectionTitleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        }

        _chart.RefreshThemeResources();
        _gauge.RefreshThemeResources();
    }

    private void RefreshLegend(MetricPanelViewModel panel)
    {
        if (panel.VisibleSeries.Count == 0)
        {
            _legendRows.Clear();
            _legendHost.Children.Clear();
            _legendScroller.Visibility = Visibility.Collapsed;
            return;
        }

        _legendScroller.Visibility = panel.SupportsProcessTable ? Visibility.Collapsed : Visibility.Visible;

        if (_legendRows.Count == 0 && _legendHost.Children.Count > 0)
        {
            _legendHost.Children.Clear();
        }

        var activeKeys = new HashSet<string>(panel.VisibleSeries.Select(series => series.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var staleKey in _legendRows.Keys.Where(key => !activeKeys.Contains(key)).ToArray())
        {
            _legendHost.Children.Remove(_legendRows[staleKey].Row);
            _legendRows.Remove(staleKey);
        }

        var orderedRows = new List<UIElement>(panel.VisibleSeries.Count);
        for (var index = 0; index < panel.VisibleSeries.Count; index++)
        {
            var series = panel.VisibleSeries[index];
            var value = series.Points.Count == 0
                ? "--"
                : FormatValue(series.Points[^1].Value, panel.Unit);

            if (!_legendRows.TryGetValue(series.Name, out var rowParts))
            {
                rowParts = CreateLegendRow(series.Name, series.StrokeBrush);
                _legendRows.Add(series.Name, rowParts);
                _legendHost.Children.Add(rowParts.Row);
            }

            rowParts.ValueText.Text = value;
            orderedRows.Add(rowParts.Row);
        }

        if (_legendHost.Children.Count != orderedRows.Count ||
            !_legendHost.Children.Cast<UIElement>().SequenceEqual(orderedRows))
        {
            _legendHost.Children.Clear();
            foreach (var row in orderedRows)
            {
                _legendHost.Children.Add(row);
            }
        }
    }

    private void RefreshProcessRows(MetricPanelViewModel panel)
    {
        if (!panel.SupportsProcessTable)
        {
            _processRowsHost.Children.Clear();
            _processRowParts.Clear();
            _processExpandText.Visibility = Visibility.Collapsed;
            return;
        }

        _processSectionTitleText.Text = panel.ProcessTableTitle;
        if (panel.ProcessRows.Count == 0)
        {
            _processRowParts.Clear();
            _processRowsHost.Children.Clear();
            _processRowsHost.Children.Add(CreateEmptyText("Process table warming up"));
            _processExpandText.Visibility = Visibility.Collapsed;
            return;
        }

        // Fast path: if row count and keys match in order, just update values in-place
        var rows = panel.ProcessRows;
        if (_processRowsHost.Children.Count == rows.Count)
        {
            var allMatch = true;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!_processRowParts.TryGetValue(rows[i].Key, out var rp) ||
                    !ReferenceEquals(_processRowsHost.Children[i], rp.Row))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                // Same rows, same order — just update text and meter values
                for (var i = 0; i < rows.Count; i++)
                {
                    var item = rows[i];
                    var rp = _processRowParts[item.Key];
                    rp.NameText.Text = item.Name;
                    rp.ValueText.Text = item.Value;
                    rp.CaptionText.Text = item.Caption;
                    rp.MeterFill.Width = 32 * Math.Clamp(item.Intensity, 0d, 1d);
                }

                UpdateProcessExpandButton(panel);
                return;
            }
        }

        // Slow path: rebuild the row list (sort change, new/removed processes)
        _processRowsHost.Children.Clear();
        _processRowParts.Clear();

        for (var i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            var rowParts = CreateProcessRow(item, panel.AccentBrush);
            _processRowParts.Add(item.Key, rowParts);
            _processRowsHost.Children.Add(rowParts.Row);
        }

        UpdateProcessExpandButton(panel);
    }

    private void UpdateProcessExpandButton(MetricPanelViewModel panel)
    {
        var total = panel.TotalProcessCount;
        var showing = panel.ProcessRows.Count;
        if (total > showing)
        {
            _processExpandText.Text = $"Show all {total} processes";
            _processExpandText.Visibility = Visibility.Visible;
        }
        else if (panel.ProcessListExpanded && total > 30)
        {
            _processExpandText.Text = "Show fewer";
            _processExpandText.Visibility = Visibility.Visible;
        }
        else
        {
            _processExpandText.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshVisualMode(MetricPanelViewModel? panel)
    {
        var showGauge = panel?.PrefersGaugeVisual == true;
        _gauge.Visibility = showGauge ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(_chartFrame, showGauge ? 1 : 0);
        Grid.SetColumnSpan(_chartFrame, showGauge ? 1 : 2);
        _legendScroller.Visibility = panel?.SupportsProcessTable == true ? Visibility.Collapsed : Visibility.Visible;

        if (panel?.SupportsProcessTable == true)
        {
            if (!_processSectionRevealed)
            {
                // First reveal — animate smoothly from zero height
                _processSectionRevealed = true;
                _processSection.Visibility = Visibility.Visible;
                _processSection.Opacity = 0;
                _processSection.MaxHeight = 0;
                _processSection.RenderTransform = new TranslateTransform();

                // Defer animation to after layout measures the section
                _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                    var heightAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = 500,
                        Duration = new Duration(TimeSpan.FromMilliseconds(450)),
                        EasingFunction = ease,
                    };

                    var fadeAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        EasingFunction = ease,
                    };

                    var slideAnim = new DoubleAnimation
                    {
                        From = 6,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(450)),
                        EasingFunction = ease,
                    };

                    Storyboard.SetTarget(heightAnim, _processSection);
                    Storyboard.SetTargetProperty(heightAnim, "MaxHeight");
                    Storyboard.SetTarget(fadeAnim, _processSection);
                    Storyboard.SetTargetProperty(fadeAnim, "Opacity");
                    Storyboard.SetTarget(slideAnim, _processSection.RenderTransform);
                    Storyboard.SetTargetProperty(slideAnim, "Y");

                    var sb = new Storyboard();
                    sb.Children.Add(heightAnim);
                    sb.Children.Add(fadeAnim);
                    sb.Children.Add(slideAnim);
                    sb.Completed += (_, _) => _processSection.MaxHeight = double.PositiveInfinity;
                    sb.Begin();
                });
            }
            else
            {
                _processSection.Visibility = Visibility.Visible;
            }
        }
        else
        {
            _processSection.Visibility = Visibility.Collapsed;
            _processSectionRevealed = false;
        }
    }

    private void RefreshRangeButtons(MetricPanelViewModel? panel)
    {
        var allowPresetHighlight = panel?.IsZoomed != true;
        ApplyRangeState(_oneMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(_fiveMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(_fifteenMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(_oneHourButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private void RefreshProcessSortButtons(MetricPanelViewModel? panel)
    {
        ApplyRangeState(_sortHighestButton, panel?.ProcessSortMode == ProcessSortMode.Highest);
        ApplyRangeState(_sortLowestButton, panel?.ProcessSortMode == ProcessSortMode.Lowest);
        ApplyRangeState(_sortNameButton, panel?.ProcessSortMode == ProcessSortMode.Name);
    }

    private void ApplyPalette(MetricPanelViewModel panel)
    {
        _badgeBorder.Background = CreateSurfaceGradient("#102131", "#17304A");
        _badgeBorder.BorderBrush = panel.AccentBrush;
        _edgeGlow.Background = panel.AccentBrush;
        _footerText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _scaleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        _titleText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        _processSectionTitleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");

        // Color-code current value based on utilization thresholds
        var util = panel.UtilizationPercent;
        if (util > 90)
        {
            _currentValueText.Foreground = BrushFactory.CreateBrush("#FF8C42"); // orange — critical
        }
        else if (util > 75)
        {
            _currentValueText.Foreground = BrushFactory.CreateBrush("#FFD166"); // yellow — elevated
        }
        else
        {
            _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"); // default
        }
    }

    private void SetHoverState(bool isHovered)
    {
        if (_isDropTarget || _isHeaderDragActive)
        {
            return;
        }

        _cardBorder.BorderBrush = isHovered
            ? ResolveBrush("AccentBrush", "#66E7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _cardBorder.BorderThickness = new Thickness(isHovered ? 1.5 : 1.2);
        _cardBorder.Opacity = 1.0;
        _cardBorder.Background = isHovered
            ? CreateSurfaceGradient("#102133", "#162A40")
            : CreateSurfaceGradient("#0E1B2C", "#13253A");
    }

    private ActionChip CreateRangeButton(string text, TimeRangePreset preset)
    {
        var button = new ActionChip
        {
            Tag = preset,
            MinHeight = 30,
            MinWidth = 40,
            Text = text,
        };

        button.Click += OnRangeClick;
        return button;
    }

    private ActionChip CreateProcessSortButton(string text, ProcessSortMode sortMode)
    {
        var button = new ActionChip
        {
            Tag = sortMode,
            MinHeight = 28,
            MinWidth = 58,
            Text = text,
        };

        button.Click += OnProcessSortClick;
        return button;
    }

    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Panel is null || IsInteractiveHeaderElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isHeaderPointerDown = true;
        _isHeaderDragActive = false;

        if (XamlRoot?.Content is UIElement root)
        {
            _dragMouseStart = e.GetCurrentPoint(root).Position;
        }

        this.CancelDirectManipulations();
        FindAncestor<ScrollViewer>(this)?.CancelDirectManipulations();
        if (sender is UIElement senderElement)
        {
            senderElement.CapturePointer(e.Pointer);
        }

        e.Handled = true;
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isHeaderPointerDown || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        var rootPoint = e.GetCurrentPoint(root).Position;

        if (!_isHeaderDragActive)
        {
            var dx = rootPoint.X - _dragMouseStart.X;
            var dy = rootPoint.Y - _dragMouseStart.Y;
            if ((dx * dx) + (dy * dy) < 64d)
            {
                return;
            }

            _isHeaderDragActive = true;

            _cardBorder.BorderBrush = ResolveBrush("AccentStrongBrush", "#9FEFFF");
            _cardBorder.BorderThickness = new Thickness(2);
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            Canvas.SetZIndex(this, 50);
            CacheDragTargets();

            // Animate lift — smooth transition into drag state
            var liftOpacity = new DoubleAnimation
            {
                To = 0.88,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(liftOpacity, _cardBorder);
            Storyboard.SetTargetProperty(liftOpacity, "Opacity");
            var liftStoryboard = new Storyboard();
            liftStoryboard.Children.Add(liftOpacity);
            liftStoryboard.Begin();
        }

        // Pure mouse delta — card moves exactly with the mouse, no feedback loops
        _dragTranslate.X = rootPoint.X - _dragMouseStart.X;
        _dragTranslate.Y = rootPoint.Y - _dragMouseStart.Y;

        // When pointer enters a different panel, swap positions (with cooldown to prevent chain-swaps)
        var targetCard = ResolveDropTargetCard(e);
        var now = Environment.TickCount64;

        if (!ReferenceEquals(targetCard, _activeDropTargetCard))
        {
            _activeDropTargetCard?.SetDropTargetState(false);

            if (targetCard is not null && Panel is not null && (now - _lastSwapTicks) > 250)
            {
                _activeDropTargetCard = targetCard;
                targetCard.SetDropTargetState(true);

                var targetKey = targetCard.Panel?.PanelKey;
                if (!string.IsNullOrWhiteSpace(targetKey))
                {
                    _lastSwapTicks = now;
                    PanelReorderRequested?.Invoke(this, new PanelReorderRequestedEventArgs(Panel.PanelKey, targetKey));
                    // Recache after the grid repositions
                    _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        if (_isHeaderDragActive)
                        {
                            CacheDragTargets();
                        }
                    });
                }
            }
            else
            {
                _activeDropTargetCard = null;
            }
        }

        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isHeaderPointerDown)
        {
            return;
        }

        EndHeaderDragInteraction();
        if (sender is UIElement senderElement)
        {
            senderElement.ReleasePointerCaptures();
        }

        e.Handled = true;
    }

    private void OnHeaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_isHeaderPointerDown)
        {
            EndHeaderDragInteraction();
        }
    }

    private void OnRangeClick(object? sender, EventArgs e)
    {
        if (sender is not ActionChip { Tag: TimeRangePreset preset })
        {
            return;
        }

        if (RangePresetRequested is not null)
        {
            RangePresetRequested.Invoke(this, new TimeRangePresetRequestedEventArgs(preset));
            return;
        }

        Panel?.ApplyRangePreset(preset);
    }

    private void OnProcessExpandToggle(object sender, TappedRoutedEventArgs e)
    {
        if (Panel is { } panel)
        {
            panel.ProcessListExpanded = !panel.ProcessListExpanded;
        }
    }

    private void OnPerProcessChartToggle(object? sender, EventArgs e)
    {
        if (Panel is { } panel)
        {
            panel.PerProcessChartsEnabled = !panel.PerProcessChartsEnabled;
            _perProcessChartButton.IsActive = panel.PerProcessChartsEnabled;
        }
    }

    private void OnProcessSortClick(object? sender, EventArgs e)
    {
        if (sender is not ActionChip { Tag: ProcessSortMode sortMode })
        {
            return;
        }

        if (Panel is { } panel)
        {
            // Update button states immediately for snappy feedback
            RefreshProcessSortButtons(panel);
            panel.ProcessSortMode = sortMode;
            RefreshProcessSortButtons(panel);
        }
    }

    private void OnChartZoomSelectionRequested(object? sender, ChartZoomSelectionEventArgs e)
    {
        if (PanelZoomSelectionRequested is not null)
        {
            PanelZoomSelectionRequested.Invoke(this, e);
            return;
        }

        Panel?.ZoomToWindow(e.StartUtc, e.EndUtc);
    }

    private void OnChartZoomResetRequested(object? sender, EventArgs e)
    {
        if (PanelZoomResetRequested is not null)
        {
            PanelZoomResetRequested.Invoke(this, e);
            return;
        }

        Panel?.ResetZoom();
    }

    public void PlaySwapSettleAnimation()
    {
        // Quick opacity pulse — card briefly dims then returns, indicating it just moved
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var fadeOut = new DoubleAnimation
        {
            From = 0.6,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = ease,
        };

        Storyboard.SetTarget(fadeOut, _cardBorder);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(fadeOut);
        sb.Begin();
    }

    private void SetDropTargetState(bool isActive)
    {
        _isDropTarget = isActive;
        _cardBorder.BorderBrush = isActive
            ? ResolveBrush("AccentBrush", "#66E7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _cardBorder.BorderThickness = new Thickness(isActive ? 1.5 : 1.2);
        _cardBorder.Background = isActive
            ? CreateSurfaceGradient("#102133", "#162A40")
            : CreateSurfaceGradient("#0E1B2C", "#13253A");

        // Animate the opacity pulse for smooth feedback
        var targetOpacity = isActive ? 0.92 : 1.0;
        var opAnim = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(120)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(opAnim, _cardBorder);
        Storyboard.SetTargetProperty(opAnim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(opAnim);
        sb.Begin();
    }

    private void CacheDragTargets()
    {
        _dragTargetCache = [];
        if (XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        foreach (var card in EnumeratePanelCards(root))
        {
            if (ReferenceEquals(card, this) || card.Panel is null)
            {
                continue;
            }

            if (TryGetBounds(card, root, out var bounds))
            {
                _dragTargetCache.Add((card, bounds));
            }
        }
    }

    private void UpdateDropTargetState(PointerRoutedEventArgs e)
    {
        var targetCard = ResolveDropTargetCard(e);
        if (ReferenceEquals(_activeDropTargetCard, targetCard))
        {
            return;
        }

        _activeDropTargetCard?.SetDropTargetState(false);
        _activeDropTargetCard = targetCard;
        _activeDropTargetCard?.SetDropTargetState(true);
    }

    private TelemetryPanelCard? ResolveDropTargetCard(PointerRoutedEventArgs e)
    {
        if (_dragTargetCache is null || _dragTargetCache.Count == 0 || XamlRoot?.Content is not UIElement root)
        {
            return null;
        }

        var hostPoint = e.GetCurrentPoint(root).Position;

        // Direct hit — pointer must be inside or very close to a card to trigger swap
        TelemetryPanelCard? nearestCard = null;
        var nearestDistance = double.MaxValue;

        foreach (var (card, bounds) in _dragTargetCache)
        {
            // Direct hit
            if (bounds.Contains(hostPoint))
            {
                return card;
            }

            // Within a small margin of the card edge (20px)
            var dx = Math.Max(0, Math.Max(bounds.X - hostPoint.X, hostPoint.X - (bounds.X + bounds.Width)));
            var dy = Math.Max(0, Math.Max(bounds.Y - hostPoint.Y, hostPoint.Y - (bounds.Y + bounds.Height)));
            var dist = (dx * dx) + (dy * dy);

            if (dist <= 400 && dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestCard = card;
            }
        }

        return nearestCard;
    }

    private void EndHeaderDragInteraction()
    {
        var wasDragging = _isHeaderDragActive;
        _isHeaderPointerDown = false;
        _isHeaderDragActive = false;
        _activeDropTargetCard?.SetDropTargetState(false);
        _activeDropTargetCard = null;
        _dragTargetCache = null;

        if (wasDragging)
        {
            ProtectedCursor = null;
            _cardBorder.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            _cardBorder.BorderThickness = new Thickness(1.2);

            // Finalize grid position and clear translate instantly — no swoop
            PanelDragEnded?.Invoke(this, EventArgs.Empty);
            _dragTranslate.X = 0;
            _dragTranslate.Y = 0;

            // Subtle opacity pulse to confirm placement
            var opacityAnim = new DoubleAnimation
            {
                From = 0.7,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(opacityAnim, _cardBorder);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnim);
            storyboard.Completed += (_, _) => Canvas.SetZIndex(this, 0);
            storyboard.Begin();
        }
        else
        {
            _dragTranslate.X = 0;
            _dragTranslate.Y = 0;
        }
    }

    private static bool IsInteractiveHeaderElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ActionChip)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : class
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static IEnumerable<TelemetryPanelCard> EnumeratePanelCards(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TelemetryPanelCard panelCard)
            {
                yield return panelCard;
            }

            foreach (var nested in EnumeratePanelCards(child))
            {
                yield return nested;
            }
        }
    }

    private static bool TryGetBounds(FrameworkElement element, UIElement root, out Windows.Foundation.Rect bounds)
    {
        bounds = default;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var transform = element.TransformToVisual(root);
            var topLeft = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            bounds = new Windows.Foundation.Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (Border Row, TextBlock NameText, TextBlock ValueText, Ellipse Dot) CreateLegendRow(string name, Brush strokeBrush)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 3, 9, 0),
            Fill = strokeBrush,
        };
        row.Children.Add(dot);

        var nameText = new TextBlock
        {
            FontSize = 11,
            Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
            Text = name,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        row.Children.Add(nameText);
        Grid.SetColumn(nameText, 1);

        var valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = "--",
            MinWidth = 42,
            TextAlignment = TextAlignment.Right,
        };
        row.Children.Add(valueText);
        Grid.SetColumn(valueText, 2);

        return (new Border
        {
            Background = CreateSurfaceGradient("#101C2D", "#132438"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Child = row,
        }, nameText, valueText, dot);
    }

    private static (Border Row, TextBlock NameText, TextBlock ValueText, TextBlock CaptionText, Border MeterFill, Border MeterTrack) CreateProcessRow(ProcessListItemViewModel item, Brush accentBrush)
    {
        var nameText = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 11.5,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var captionText = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 9.5,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = item.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        // Inline meter bar right next to the value
        var meterFill = new Border
        {
            Width = 32 * Math.Clamp(item.Intensity, 0d, 1d),
            Height = 3,
            Background = accentBrush,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var meterTrack = new Border
        {
            Width = 32,
            Height = 3,
            Background = ResolveBrush("SurfaceStrongBrush", "#162A41"),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Child = meterFill,
        };

        var valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Value,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 46,
            TextAlignment = TextAlignment.Right,
        };

        // Clean table row: Name | Caption | Meter+Value (right-aligned)
        var rowGrid = new Grid
        {
            ColumnSpacing = 6,
        };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 90 });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        rowGrid.Children.Add(nameText);
        rowGrid.Children.Add(captionText);
        Grid.SetColumn(captionText, 1);
        rowGrid.Children.Add(meterTrack);
        Grid.SetColumn(meterTrack, 2);
        rowGrid.Children.Add(valueText);
        Grid.SetColumn(valueText, 3);

        // Subtle bottom separator instead of bordered card
        return (new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(0, 0, 0, 0.5),
            Child = rowGrid,
        }, nameText, valueText, captionText, meterFill, meterTrack);
    }

    private void UpdateBadgeIcon(MetricPanelViewModel? panel)
    {
        var iconKey = panel switch
        {
            null => "collection",
            { PanelKey: var key } when key.Contains("temperature", StringComparison.OrdinalIgnoreCase) => "temperature",
            { Category: MetricCategory.Cpu } => "cpu",
            { Category: MetricCategory.Gpu } => "gpu",
            { Category: MetricCategory.Memory } => "memory",
            { Category: MetricCategory.Disk, PrefersGaugeVisual: true } => "drive",
            { Category: MetricCategory.Disk } => "disk",
            { Category: MetricCategory.Network } => "network",
            { Category: MetricCategory.System } => "system",
            _ => "collection",
        };

        var accentBrush = panel?.AccentBrush ?? ResolveBrush("AccentBrush", "#66E7FF");
        _badgeIconHost.Children.Clear();
        _badgeIconHost.Children.Add(IconFactory.CreateIcon(iconKey, accentBrush, 16));
    }

    private static void ApplyRangeState(ActionChip control, bool isActive)
    {
        control.IsActive = isActive;
        control.IsFilled = isActive;
        control.Opacity = isActive ? 1 : 0.76;
    }

    private static TextBlock CreateTextBlock(string? fontFamily = null, double fontSize = 12, Windows.UI.Text.FontWeight? fontWeight = null) =>
        new()
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? new FontFamily("Segoe UI Variable Text") : new FontFamily(fontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateEmptyText(string text) =>
        new()
        {
            FontSize = 11,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = text,
        };

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
    }

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
        if (IsLightPaletteActive())
        {
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = ResolveColor("SurfaceBrush", "#F8FBFF"), Offset = 0d },
                    new GradientStop { Color = ResolveColor("SurfaceElevatedBrush", "#EEF5FB"), Offset = 1d },
                },
            };
        }

        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = BrushFactory.ParseColor(startHex), Offset = 0d },
                new GradientStop { Color = BrushFactory.ParseColor(endHex), Offset = 1d },
            },
        };
    }

    private static bool IsLightPaletteActive()
    {
        var color = ResolveColor("AppBackdropBrush", "#030812");
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance >= 170d;
    }

    private static Windows.UI.Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return BrushFactory.ParseColor(fallbackHex);
    }
    private static string FormatValue(double value, MetricUnit unit) => unit switch
    {
        MetricUnit.Percent => $"{value:0.#}%",
        MetricUnit.Celsius => $"{value:0.#} C",
        MetricUnit.Gigabytes when value >= 1024d => $"{value / 1024d:0.0} TiB",
        MetricUnit.Gigabytes => $"{value:0.0} GiB",
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        MetricUnit.Count when value >= 1_000_000d => $"{value / 1_000_000d:0.#}M",
        MetricUnit.Count when value >= 1_000d => $"{value / 1_000d:0.#}k",
        MetricUnit.Count => $"{value:0}",
        _ => $"{value:0.##}",
    };

    private void PlayEntranceAnimation()
    {
        var col = Grid.GetColumn(this);
        var row = Grid.GetRow(this);
        var staggerMs = (row * 3 + col) * 70 + 60;

        // Slide from the direction the card is positioned:
        // Cards on the left side of the window slide from left, right side from right
        var slideX = 0d;
        if (XamlRoot?.Content is UIElement root && ActualWidth > 0)
        {
            var cardCenter = TransformToVisual(root).TransformPoint(
                new Windows.Foundation.Point(ActualWidth / 2d, 0)).X;
            var windowCenter = root.ActualSize.X / 2d;
            slideX = cardCenter < windowCenter ? 3d : -3d;
        }
        var slideY = 2d;

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var translate = new TranslateTransform { X = slideX, Y = slideY };
        _cardBorder.RenderTransform = translate;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(350)),
            BeginTime = TimeSpan.FromMilliseconds(staggerMs),
            EasingFunction = ease,
        };

        var moveX = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(350)),
            BeginTime = TimeSpan.FromMilliseconds(staggerMs),
            EasingFunction = ease,
        };

        var moveY = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(350)),
            BeginTime = TimeSpan.FromMilliseconds(staggerMs),
            EasingFunction = ease,
        };

        Storyboard.SetTarget(fadeIn, this);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        Storyboard.SetTarget(moveX, translate);
        Storyboard.SetTargetProperty(moveX, "X");
        Storyboard.SetTarget(moveY, translate);
        Storyboard.SetTargetProperty(moveY, "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        storyboard.Children.Add(moveX);
        storyboard.Children.Add(moveY);
        storyboard.Completed += (_, _) =>
        {
            _cardBorder.RenderTransform = null;
        };
        storyboard.Begin();
    }
}

public sealed class TimeRangePresetRequestedEventArgs : EventArgs
{
    public TimeRangePresetRequestedEventArgs(TimeRangePreset preset)
    {
        Preset = preset;
    }

    public TimeRangePreset Preset { get; }
}

public sealed class PanelReorderRequestedEventArgs : EventArgs
{
    public PanelReorderRequestedEventArgs(string sourcePanelKey, string targetPanelKey)
    {
        SourcePanelKey = sourcePanelKey;
        TargetPanelKey = targetPanelKey;
    }

    public string SourcePanelKey { get; }

    public string TargetPanelKey { get; }
}
