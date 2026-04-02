using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
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
    private readonly StackPanel _legendHost;
    private readonly ScrollViewer _legendScroller;
    private readonly Border _processSection;
    private readonly TextBlock _processSectionTitleText;
    private readonly StackPanel _processRowsHost;
    private readonly ScrollViewer _processScroller;
    private readonly ActionChip _oneMinuteButton;
    private readonly ActionChip _fiveMinuteButton;
    private readonly ActionChip _fifteenMinuteButton;
    private readonly ActionChip _oneHourButton;
    private readonly ActionChip _sortHighestButton;
    private readonly ActionChip _sortLowestButton;
    private readonly ActionChip _sortNameButton;
    private readonly Dictionary<string, (Border Row, TextBlock ValueText)> _legendRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Border Row, TextBlock NameText, TextBlock ValueText, TextBlock CaptionText, Border MeterFill)> _processRowParts = new(StringComparer.OrdinalIgnoreCase);
    private MetricPanelViewModel? _observedPanel;
    private bool _refreshQueued;

    public TelemetryPanelCard()
    {
        MinHeight = 372;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _badgeIconHost = new Grid
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _footerText = CreateTextBlock(fontSize: 11);
        _scaleText = CreateTextBlock("Segoe UI Variable Text", 11, FontWeights.Medium);
        _titleText = CreateTextBlock("Segoe UI Variable Display", 18, FontWeights.SemiBold);
        _currentValueText = CreateTextBlock("Segoe UI Variable Display", 22, FontWeights.SemiBold);
        _secondaryValueText = CreateTextBlock(fontSize: 12);

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
            ColumnSpacing = 16,
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
            Height = 180,
            MinHeight = 180,
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
            MaxHeight = 192,
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
            Spacing = 6,
        };
        _processScroller = new ScrollViewer
        {
            MaxHeight = 222,
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

        var processLabel = CreateTextBlock(fontSize: 10);
        processLabel.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        processLabel.CharacterSpacing = 60;
        processLabel.Text = "PROCESS";
        processColumns.Children.Add(processLabel);

        var sparkLabel = CreateTextBlock(fontSize: 10);
        sparkLabel.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        sparkLabel.CharacterSpacing = 60;
        sparkLabel.Text = "ACTIVITY";
        processColumns.Children.Add(sparkLabel);
        Grid.SetColumn(sparkLabel, 1);

        var valueLabel = CreateTextBlock(fontSize: 10);
        valueLabel.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        valueLabel.CharacterSpacing = 60;
        valueLabel.Text = "VALUE";
        processColumns.Children.Add(valueLabel);
        Grid.SetColumn(valueLabel, 2);

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

        var rangeShell = new Border
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

        var metaGrid = new Grid();
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
        var scalePill = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 4, 9, 4),
            Child = _scaleText,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        metaGrid.Children.Add(scalePill);
        Grid.SetColumn(scalePill, 1);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                metaGrid,
                _titleText,
                _currentValueText,
                _secondaryValueText,
            },
        });
        headerGrid.Children.Add(rangeShell);
        Grid.SetColumn(rangeShell, 1);

        _cardBorder = new Border
        {
            Background = CreateSurfaceGradient("#0E1B2C", "#13253A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16, 15, 16, 15),
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

        Content = _cardBorder;
        Loaded += (_, _) => RefreshFromPanel();
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
            _titleText.Text = "Telemetry";
            _currentValueText.Text = "Waiting";
            _secondaryValueText.Text = "Collecting hardware samples";
            _chart.Series = Array.Empty<ChartSeriesViewModel>();
            _chart.CeilingValue = 0d;
            _legendHost.Children.Clear();
            _legendRows.Clear();
            _legendHost.Children.Add(CreateEmptyText("Waiting for samples"));
            _processRowsHost.Children.Clear();
            _processRowParts.Clear();
            _processSection.Visibility = Visibility.Collapsed;
            RefreshRangeButtons(null);
            RefreshProcessSortButtons(null);
            RefreshVisualMode(null);
            return;
        }

        UpdateBadgeIcon(panel);
        _footerText.Text = panel.FooterText;
        _titleText.Text = panel.Title;
        _currentValueText.Text = panel.CurrentValue;
        _secondaryValueText.Text = panel.SecondaryValue;
        _scaleText.Text = panel.ScaleLabel;

        _chart.Series = panel.VisibleSeries;
        _chart.Unit = panel.Unit;
        _chart.WindowStartUtc = panel.WindowStartUtc;
        _chart.WindowEndUtc = panel.WindowEndUtc;
        _chart.CeilingValue = panel.ChartCeilingValue;

        _gauge.Value = panel.GaugeValue;
        _gauge.AccentBrush = panel.AccentBrush;
        _gauge.Caption = panel.PrefersGaugeVisual ? "Capacity" : "Live";

        RefreshLegend(panel);
        RefreshProcessRows(panel);
        RefreshRangeButtons(panel);
        RefreshProcessSortButtons(panel);
        RefreshVisualMode(panel);
        ApplyPalette(panel);
    }

    private void RefreshLegend(MetricPanelViewModel panel)
    {
        if (panel.VisibleSeries.Count == 0)
        {
            _legendRows.Clear();
            _legendHost.Children.Clear();
            _legendHost.Children.Add(CreateEmptyText("Waiting for samples"));
            return;
        }

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
            if (_legendHost.Children.IndexOf(rowParts.Row) != index)
            {
                _legendHost.Children.Remove(rowParts.Row);
                _legendHost.Children.Insert(index, rowParts.Row);
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
            rowParts.MeterFill.Width = 72 * Math.Clamp(item.Intensity, 0d, 1d);
            if (_processRowsHost.Children.IndexOf(rowParts.Row) != index)
            {
                _processRowsHost.Children.Remove(rowParts.Row);
                _processRowsHost.Children.Insert(index, rowParts.Row);
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
        _scaleText.Foreground = ResolveBrush("AccentStrongBrush", "#B7F7FF");
        _titleText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        _processSectionTitleText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
    }

    private void SetHoverState(bool isHovered)
    {
        _cardBorder.BorderBrush = isHovered
            ? ResolveBrush("AccentStrongBrush", "#9FEFFF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _cardBorder.Opacity = 1.0;
        _cardBorder.Background = isHovered
            ? CreateSurfaceGradient("#102133", "#162A40")
            : CreateSurfaceGradient("#0E1B2C", "#13253A");
    }

    private static ActionChip CreateRangeButton(string text, TimeRangePreset preset)
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

    private static ActionChip CreateProcessSortButton(string text, ProcessSortMode sortMode)
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

    private static void OnRangeClick(object? sender, EventArgs e)
    {
        if (sender is ActionChip { Tag: TimeRangePreset preset } button &&
            button.Parent is FrameworkElement parent)
        {
            var card = FindParent<TelemetryPanelCard>(parent);
            if (card?.Panel is not null)
            {
                card.Panel.ApplyRangePreset(preset);
            }
        }
    }

    private static void OnProcessSortClick(object? sender, EventArgs e)
    {
        if (sender is ActionChip { Tag: ProcessSortMode sortMode } button &&
            button.Parent is FrameworkElement parent)
        {
            var card = FindParent<TelemetryPanelCard>(parent);
            if (card?.Panel is not null)
            {
                card.Panel.ProcessSortMode = sortMode;
            }
        }
    }

    private void OnChartZoomSelectionRequested(object? sender, ChartZoomSelectionEventArgs e)
    {
        Panel?.ZoomToWindow(e.StartUtc, e.EndUtc);
    }

    private void OnChartZoomResetRequested(object? sender, EventArgs e)
    {
        Panel?.ResetZoom();
    }

    private static (Border Row, TextBlock ValueText) CreateLegendRow(string name, Brush strokeBrush)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 3, 9, 0),
            Fill = strokeBrush,
        });

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
            Padding = new Thickness(12, 9, 12, 9),
            Child = row,
        }, valueText);
    }

    private static (Border Row, TextBlock NameText, TextBlock ValueText, TextBlock CaptionText, Border MeterFill) CreateProcessRow(ProcessListItemViewModel item, Brush accentBrush)
    {
        var nameText = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var captionText = new TextBlock
        {
            FontSize = 10.5,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = item.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = item.Value,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var rowGrid = new Grid
        {
            ColumnSpacing = 10,
        };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rowGrid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                nameText,
                captionText,
            },
        });

        var meterFill = new Border
        {
            Width = 72 * Math.Clamp(item.Intensity, 0d, 1d),
            Height = 6,
            Background = accentBrush,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var meterTrack = new Border
        {
            Width = 72,
            Height = 6,
            Background = ResolveBrush("SurfaceStrongBrush", "#162A41"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Child = meterFill,
        };
        rowGrid.Children.Add(meterTrack);
        Grid.SetColumn(meterTrack, 1);

        var valueHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = accentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                valueText,
            },
        };
        rowGrid.Children.Add(valueHost);
        Grid.SetColumn(valueHost, 2);

        return (new Border
        {
            Background = CreateSurfaceGradient("#101C2D", "#132438"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(12, 10, 12, 10),
            Child = rowGrid,
        }, nameText, valueText, captionText, meterFill);
    }

    private void UpdateBadgeIcon(MetricPanelViewModel? panel)
    {
        var iconKey = panel switch
        {
            null => "collection",
            { Category: MetricCategory.Cpu } => "cpu",
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

    private static T? FindParent<T>(DependencyObject? start)
        where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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
}
