using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Dispatching;
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
    private readonly Dictionary<string, (Border Row, TextBlock NameText, TextBlock ValueText, Ellipse Dot)> _legendRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Border Row, TextBlock NameText, TextBlock ValueText, TextBlock CaptionText, Border MeterFill, Border MeterTrack)> _processRowParts = new(StringComparer.OrdinalIgnoreCase);
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
    private readonly TranslateTransform _dragTransform = new();
    private Windows.Foundation.Point _headerPointerStart;
    private TelemetryPanelCard? _activeDropTargetCard;
    private List<(TelemetryPanelCard Card, Windows.Foundation.Rect Bounds)>? _dragTargetCache;
    private bool _isEffectivelyVisible = true;
    private bool _isRenderingSuspended;
    private bool _deferredRefreshPending;

    public event EventHandler<TimeRangePresetRequestedEventArgs>? RangePresetRequested;

    public event EventHandler<ChartZoomSelectionEventArgs>? PanelZoomSelectionRequested;

    public event EventHandler? PanelZoomResetRequested;

    public event EventHandler<PanelReorderRequestedEventArgs>? PanelReorderRequested;

    public TelemetryPanelCard()
    {
        MinHeight = 388;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        RenderTransform = _dragTransform;

        _badgeIconHost = new Grid
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _footerText = CreateTextBlock(fontSize: 10.5);
        _scaleText = CreateTextBlock("Segoe UI Variable Text", 10.5, FontWeights.Medium);
        _scaleText.TextWrapping = TextWrapping.NoWrap;
        _scaleText.TextTrimming = TextTrimming.CharacterEllipsis;
        _scaleText.MaxWidth = 220;
        _titleText = CreateTextBlock("Segoe UI Variable Display", 17.5, FontWeights.SemiBold);
        _currentValueText = CreateTextBlock("Segoe UI Variable Display", 21.5, FontWeights.SemiBold);
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
            MaxHeight = 184,
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

        _processSectionTitleText = CreateTextBlock("Segoe UI Variable Text", 11, FontWeights.Medium);
        _processRowsHost = new StackPanel
        {
            Spacing = 0,
        };
        _processScroller = new ScrollViewer
        {
            MaxHeight = 232,
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
                _sortHighestButton,
                _sortLowestButton,
                _sortNameButton,
            },
        };
        processHeader.Children.Add(sortHost);
        Grid.SetColumn(sortHost, 1);

        var processColumns = new Grid
        {
            ColumnSpacing = 10,
        };
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        processColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _processLabelText = CreateTextBlock(fontSize: 10);
        _processLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _processLabelText.CharacterSpacing = 60;
        _processLabelText.Text = "PROCESS";
        processColumns.Children.Add(_processLabelText);

        _activityLabelText = CreateTextBlock(fontSize: 10);
        _activityLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _activityLabelText.CharacterSpacing = 60;
        _activityLabelText.Text = "ACTIVITY";
        processColumns.Children.Add(_activityLabelText);
        Grid.SetColumn(_activityLabelText, 1);

        _valueLabelText = CreateTextBlock(fontSize: 10);
        _valueLabelText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _valueLabelText.CharacterSpacing = 60;
        _valueLabelText.Text = "VALUE";
        processColumns.Children.Add(_valueLabelText);
        Grid.SetColumn(_valueLabelText, 2);

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
            BorderThickness = new Thickness(1),
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
        Loaded += (_, _) =>
        {
            RefreshFromPanel();
            PlayEntranceAnimation();
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

        foreach (var rowParts in _legendRows.Values)
        {
            rowParts.Row.Background = CreateSurfaceGradient("#101C2D", "#132438");
            rowParts.Row.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            rowParts.NameText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
            rowParts.ValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        }

        foreach (var rowParts in _processRowParts.Values)
        {
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
            return;
        }

        _processSectionTitleText.Text = panel.ProcessTableTitle;
        if (panel.ProcessRows.Count == 0)
        {
            _processRowParts.Clear();
            _processRowsHost.Children.Clear();
            _processRowsHost.Children.Add(CreateEmptyText("Process table warming up"));
            return;
        }

        if (_processRowParts.Count == 0 && _processRowsHost.Children.Count > 0)
        {
            _processRowsHost.Children.Clear();
        }

        var activeKeys = new HashSet<string>(panel.ProcessRows.Select(row => row.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var staleKey in _processRowParts.Keys.Where(key => !activeKeys.Contains(key)).ToArray())
        {
            _processRowsHost.Children.Remove(_processRowParts[staleKey].Row);
            _processRowParts.Remove(staleKey);
        }

        var orderedRows = new List<UIElement>(panel.ProcessRows.Count);
        for (var index = 0; index < panel.ProcessRows.Count; index++)
        {
            var item = panel.ProcessRows[index];
            if (!_processRowParts.TryGetValue(item.Key, out var rowParts))
            {
                rowParts = CreateProcessRow(item, panel.AccentBrush);
                _processRowParts.Add(item.Key, rowParts);
                _processRowsHost.Children.Add(rowParts.Row);
            }

            rowParts.NameText.Text = item.Name;
            rowParts.ValueText.Text = item.Value;
            rowParts.CaptionText.Text = item.Caption;
            rowParts.MeterFill.Width = 40 * Math.Clamp(item.Intensity, 0d, 1d);
            orderedRows.Add(rowParts.Row);
        }

        if (_processRowsHost.Children.Count != orderedRows.Count ||
            !_processRowsHost.Children.Cast<UIElement>().SequenceEqual(orderedRows))
        {
            _processRowsHost.Children.Clear();
            foreach (var row in orderedRows)
            {
                _processRowsHost.Children.Add(row);
            }
        }
    }

    private void RefreshVisualMode(MetricPanelViewModel? panel)
    {
        var showGauge = panel?.PrefersGaugeVisual == true;
        _gauge.Visibility = showGauge ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(_chartFrame, showGauge ? 1 : 0);
        Grid.SetColumnSpan(_chartFrame, showGauge ? 1 : 2);
        _legendScroller.Visibility = panel?.SupportsProcessTable == true ? Visibility.Collapsed : Visibility.Visible;
        _processSection.Visibility = panel?.SupportsProcessTable == true ? Visibility.Visible : Visibility.Collapsed;
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
        _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        _processSectionTitleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
    }

    private void SetHoverState(bool isHovered)
    {
        if (_isDropTarget || _isHeaderDragActive)
        {
            return;
        }

        _cardBorder.BorderBrush = isHovered
            ? ResolveBrush("AccentStrongBrush", "#9FEFFF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
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
            _headerPointerStart = e.GetCurrentPoint(root).Position;
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
            var dx = rootPoint.X - _headerPointerStart.X;
            var dy = rootPoint.Y - _headerPointerStart.Y;
            if ((dx * dx) + (dy * dy) < 100d)
            {
                return;
            }

            _isHeaderDragActive = true;
            _cardBorder.BorderBrush = ResolveBrush("AccentStrongBrush", "#9FEFFF");
            _cardBorder.BorderThickness = new Thickness(2);
            _cardBorder.Opacity = 0.8;
            Canvas.SetZIndex(this, 50);
            CacheDragTargets();
        }

        // Card follows the mouse
        _dragTransform.X = rootPoint.X - _headerPointerStart.X;
        _dragTransform.Y = rootPoint.Y - _headerPointerStart.Y;

        // When pointer enters a different panel, swap immediately
        var targetCard = ResolveDropTargetCard(e);
        if (targetCard is not null &&
            !ReferenceEquals(targetCard, _activeDropTargetCard) &&
            Panel is not null)
        {
            _activeDropTargetCard = targetCard;
            var targetKey = targetCard.Panel?.PanelKey;
            if (!string.IsNullOrWhiteSpace(targetKey))
            {
                PanelReorderRequested?.Invoke(this, new PanelReorderRequestedEventArgs(Panel.PanelKey, targetKey));
                // After swap, the grid reflows. Reset the drag origin so the card
                // snaps to its new grid position and continues following from there.
                _headerPointerStart = rootPoint;
                _dragTransform.X = 0;
                _dragTransform.Y = 0;
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, CacheDragTargets);
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

    private void OnProcessSortClick(object? sender, EventArgs e)
    {
        if (sender is not ActionChip { Tag: ProcessSortMode sortMode })
        {
            return;
        }

        if (Panel is { } panel)
        {
            panel.ProcessSortMode = sortMode;
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

    private void SetDropTargetState(bool isActive)
    {
        _isDropTarget = isActive;
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
        TelemetryPanelCard? nearestCard = null;
        var nearestDistance = double.MaxValue;

        foreach (var (card, bounds) in _dragTargetCache)
        {
            if (bounds.Contains(hostPoint))
            {
                return card;
            }

            var centerX = bounds.X + (bounds.Width / 2d);
            var centerY = bounds.Y + (bounds.Height / 2d);
            var dx = centerX - hostPoint.X;
            var dy = centerY - hostPoint.Y;
            var dist = (dx * dx) + (dy * dy);
            if (dist < nearestDistance)
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
        _activeDropTargetCard = null;
        _dragTargetCache = null;
        _dragTransform.X = 0;
        _dragTransform.Y = 0;

        if (wasDragging)
        {
            _cardBorder.Opacity = 1.0;
            _cardBorder.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            _cardBorder.BorderThickness = new Thickness(1);
            Canvas.SetZIndex(this, 0);
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
            FontSize = 12,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var captionText = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 10,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = item.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Value,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 52,
            TextAlignment = TextAlignment.Right,
        };

        var meterFill = new Border
        {
            Width = 40 * Math.Clamp(item.Intensity, 0d, 1d),
            Height = 3,
            Background = accentBrush,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var meterTrack = new Border
        {
            Width = 40,
            Height = 3,
            Background = ResolveBrush("SurfaceStrongBrush", "#162A41"),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Child = meterFill,
        };

        var rowGrid = new Grid
        {
            ColumnSpacing = 8,
        };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        rowGrid.Children.Add(nameText);
        rowGrid.Children.Add(captionText);
        Grid.SetColumn(captionText, 1);
        rowGrid.Children.Add(meterTrack);
        Grid.SetColumn(meterTrack, 2);
        rowGrid.Children.Add(valueText);
        Grid.SetColumn(valueText, 3);

        return (new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
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
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(280)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var slideUp = new DoubleAnimation
        {
            From = 12,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(320)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var translate = new TranslateTransform();
        _cardBorder.RenderTransform = translate;

        Storyboard.SetTarget(fadeIn, this);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        Storyboard.SetTarget(slideUp, translate);
        Storyboard.SetTargetProperty(slideUp, "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        storyboard.Children.Add(slideUp);
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
