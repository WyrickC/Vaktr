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
    private readonly Border _accentBar;
    private readonly TextBlock _badgeText;
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
    private readonly ActionChip _oneMinuteButton;
    private readonly ActionChip _fiveMinuteButton;
    private readonly ActionChip _fifteenMinuteButton;
    private readonly ActionChip _oneHourButton;
    private readonly Dictionary<string, (Border Row, TextBlock ValueText)> _legendRows = new(StringComparer.OrdinalIgnoreCase);
    private MetricPanelViewModel? _observedPanel;
    private bool _refreshQueued;

    public TelemetryPanelCard()
    {
        MinHeight = 396;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _badgeText = CreateTextBlock("Bahnschrift", 11, FontWeights.SemiBold);
        _footerText = CreateTextBlock(fontSize: 11);
        _scaleText = CreateTextBlock("Bahnschrift", 11, FontWeights.SemiBold);
        _titleText = CreateTextBlock("Segoe UI", 22, FontWeights.SemiBold);
        _currentValueText = CreateTextBlock("Bahnschrift", 28, FontWeights.SemiBold);
        _secondaryValueText = CreateTextBlock(fontSize: 13);

        _badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 5, 10, 5),
            Child = _badgeText,
        };

        _accentBar = new Border
        {
            Width = 86,
            Height = 2,
            CornerRadius = new CornerRadius(1),
            Background = ResolveBrush("AccentBrush", "#66E7FF"),
            Opacity = 0.7,
        };

        _oneMinuteButton = CreateRangeButton("1m", TimeRangePreset.OneMinute);
        _fiveMinuteButton = CreateRangeButton("5m", TimeRangePreset.FiveMinutes);
        _fifteenMinuteButton = CreateRangeButton("15m", TimeRangePreset.FifteenMinutes);
        _oneHourButton = CreateRangeButton("1h", TimeRangePreset.OneHour);

        _visualGrid = new Grid
        {
            ColumnSpacing = 14,
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
            Height = 176,
            MinHeight = 176,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _chart.ZoomSelectionRequested += OnChartZoomSelectionRequested;
        _chart.ZoomResetRequested += OnChartZoomResetRequested;

        _chartFrame = new Border
        {
            Background = ResolveBrush("PanelOverlayBrush", "#11283C"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 12, 12, 12),
            Child = _chart,
        };

        _visualGrid.Children.Add(_gauge);
        _visualGrid.Children.Add(_chartFrame);
        Grid.SetColumn(_chartFrame, 1);

        _legendHost = new StackPanel
        {
            Spacing = 8,
        };
        _legendScroller = new ScrollViewer
        {
            MaxHeight = 206,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled,
            Content = _legendHost,
        };

        var rangeHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
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
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 6, 8, 6),
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
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
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
            Spacing = 8,
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
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18, 18, 18, 18),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    _accentBar,
                    headerGrid,
                    _visualGrid,
                    _legendScroller,
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
            _badgeText.Text = "LIVE";
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
            RefreshRangeButtons(null);
            RefreshVisualMode(null);
            return;
        }

        _badgeText.Text = panel.Badge;
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
        RefreshRangeButtons(panel);
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

    private void RefreshVisualMode(MetricPanelViewModel? panel)
    {
        var showGauge = panel?.PrefersGaugeVisual == true;
        _gauge.Visibility = showGauge ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(_chartFrame, showGauge ? 1 : 0);
        Grid.SetColumnSpan(_chartFrame, showGauge ? 1 : 2);
    }

    private void RefreshRangeButtons(MetricPanelViewModel? panel)
    {
        var allowPresetHighlight = panel?.IsZoomed != true;
        ApplyRangeState(_oneMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(_fiveMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(_fifteenMinuteButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(_oneHourButton, allowPresetHighlight && panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private void ApplyPalette(MetricPanelViewModel panel)
    {
        _badgeBorder.Background = ResolveBrush("SurfaceBrush", "#102131");
        _badgeBorder.BorderBrush = panel.AccentBrush;
        _accentBar.Background = panel.AccentBrush;
        _badgeText.Foreground = panel.AccentBrush;
        _footerText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _scaleText.Foreground = ResolveBrush("AccentStrongBrush", "#B7F7FF");
        _titleText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
    }

    private void SetHoverState(bool isHovered)
    {
        _cardBorder.BorderBrush = isHovered
            ? ResolveBrush("AccentStrongBrush", "#9FEFFF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _cardBorder.Opacity = 1.0;
        _cardBorder.Background = ResolveBrush(isHovered ? "SurfaceStrongBrush" : "SurfaceBrush", isHovered ? "#183148" : "#102131");
    }

    private static ActionChip CreateRangeButton(string text, TimeRangePreset preset)
    {
        var button = new ActionChip
        {
            Tag = preset,
            MinHeight = 32,
            MinWidth = 42,
            Text = text,
        };

        button.Click += OnRangeClick;
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
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 4, 10, 0),
            Fill = strokeBrush,
        });

        var nameText = new TextBlock
        {
            FontSize = 12,
            Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
            Text = name,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        row.Children.Add(nameText);
        Grid.SetColumn(nameText, 1);

        var valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Text = "--",
        };
        row.Children.Add(valueText);
        Grid.SetColumn(valueText, 2);

        return (new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 10),
            Child = row,
        }, valueText);
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
        control.Opacity = isActive ? 1 : 0.68;
    }

    private static TextBlock CreateTextBlock(string? fontFamily = null, double fontSize = 12, Windows.UI.Text.FontWeight? fontWeight = null) =>
        new()
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? new FontFamily("Segoe UI") : new FontFamily(fontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateEmptyText(string text) =>
        new()
        {
            FontSize = 12,
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
    private static string FormatValue(double value, MetricUnit unit) => unit switch
    {
        MetricUnit.Percent => $"{value:0.#}%",
        MetricUnit.Gigabytes when value >= 1024d => $"{value / 1024d:0.0} TiB",
        MetricUnit.Gigabytes => $"{value:0.0} GiB",
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        _ => $"{value:0.##}",
    };
}
