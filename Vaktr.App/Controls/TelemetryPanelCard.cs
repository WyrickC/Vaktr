using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media.Animation;
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
    private readonly TextBlock _badgeText;
    private readonly TextBlock _footerText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _currentValueText;
    private readonly TextBlock _secondaryValueText;
    private readonly TelemetryChart _chart;
    private readonly UsageGauge _gauge;
    private readonly Grid _visualGrid;
    private readonly Border _chartFrame;
    private readonly StackPanel _legendHost;
    private readonly Button _oneMinuteButton;
    private readonly Button _fiveMinuteButton;
    private readonly Button _fifteenMinuteButton;
    private readonly Button _oneHourButton;
    private MetricPanelViewModel? _observedPanel;

    public TelemetryPanelCard()
    {
        MinHeight = 360;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Transitions = new TransitionCollection
        {
            new EntranceThemeTransition
            {
                FromVerticalOffset = 18,
            },
        };

        RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        RenderTransform = new ScaleTransform
        {
            ScaleX = 1,
            ScaleY = 1,
        };

        _badgeText = CreateTextBlock("Bahnschrift", 11, FontWeights.SemiBold);
        _footerText = CreateTextBlock(fontSize: 11);
        _titleText = CreateTextBlock("Segoe UI", 22, FontWeights.SemiBold);
        _currentValueText = CreateTextBlock("Bahnschrift", 28, FontWeights.SemiBold);
        _secondaryValueText = CreateTextBlock(fontSize: 13);

        _badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4, 10, 4),
            Child = _badgeText,
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
            Width = 150,
            Height = 150,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _chart = new TelemetryChart
        {
            Height = 158,
            MinHeight = 158,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        _chartFrame = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12),
            Child = _chart,
        };

        _visualGrid.Children.Add(_gauge);
        _visualGrid.Children.Add(_chartFrame);
        Grid.SetColumn(_chartFrame, 1);

        _legendHost = new StackPanel
        {
            Spacing = 8,
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

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        _badgeBorder,
                        _footerText,
                    },
                },
                _titleText,
                _currentValueText,
                _secondaryValueText,
            },
        });
        headerGrid.Children.Add(rangeHost);
        Grid.SetColumn(rangeHost, 1);

        _cardBorder = new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    headerGrid,
                    _visualGrid,
                    _legendHost,
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
        _ = DispatcherQueue.TryEnqueue(RefreshFromPanel);
    }

    private void RefreshFromPanel()
    {
        var panel = Panel;
        if (panel is null)
        {
            _badgeText.Text = "LIVE";
            _footerText.Text = string.Empty;
            _titleText.Text = "Telemetry";
            _currentValueText.Text = "Waiting";
            _secondaryValueText.Text = "Collecting hardware samples";
            _chart.Series = Array.Empty<ChartSeriesViewModel>();
            _legendHost.Children.Clear();
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

        _chart.Series = panel.VisibleSeries;
        _chart.Unit = panel.Unit;
        _chart.WindowStartUtc = panel.WindowStartUtc;
        _chart.WindowEndUtc = panel.WindowEndUtc;

        _gauge.Value = panel.GaugeValue;
        _gauge.AccentBrush = panel.AccentBrush;
        _gauge.Caption = panel.PrefersGaugeVisual ? "Drive usage" : "Live";

        RefreshLegend(panel);
        RefreshRangeButtons(panel);
        RefreshVisualMode(panel);
        ApplyPalette(panel);
    }

    private void RefreshLegend(MetricPanelViewModel panel)
    {
        _legendHost.Children.Clear();

        if (panel.VisibleSeries.Count == 0)
        {
            _legendHost.Children.Add(CreateEmptyText("Waiting for samples"));
            return;
        }

        foreach (var series in panel.VisibleSeries.Take(4))
        {
            var value = series.Points.Count == 0
                ? "--"
                : FormatValue(series.Points[^1].Value, panel.Unit);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(0, 4, 10, 0),
                Fill = series.StrokeBrush,
            });

            var nameText = new TextBlock
            {
                FontSize = 12,
                Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
                Text = series.Name,
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
                Text = value,
            };
            row.Children.Add(valueText);
            Grid.SetColumn(valueText, 2);

            _legendHost.Children.Add(new Border
            {
                Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 10),
                Child = row,
            });
        }

        if (panel.VisibleSeries.Count > 4)
        {
            _legendHost.Children.Add(CreateEmptyText($"+{panel.VisibleSeries.Count - 4} more series"));
        }
    }

    private void RefreshVisualMode(MetricPanelViewModel? panel)
    {
        var showGauge = panel?.PrefersGaugeVisual == true;
        _gauge.Visibility = showGauge ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(_chartFrame, showGauge ? 1 : 2);
    }

    private void RefreshRangeButtons(MetricPanelViewModel? panel)
    {
        ApplyRangeState(_oneMinuteButton, panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(_fiveMinuteButton, panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(_fifteenMinuteButton, panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(_oneHourButton, panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private void ApplyPalette(MetricPanelViewModel panel)
    {
        _badgeBorder.Background = panel.AccentBrush;
        _badgeBorder.Opacity = 0.18;
        _badgeText.Foreground = panel.AccentBrush;
        _footerText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _titleText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _currentValueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _secondaryValueText.Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
    }

    private void SetHoverState(bool isHovered)
    {
        if (RenderTransform is not ScaleTransform transform)
        {
            return;
        }

        transform.ScaleX = isHovered ? 1.01 : 1;
        transform.ScaleY = isHovered ? 1.01 : 1;
        _cardBorder.BorderBrush = isHovered
            ? ResolveBrush("AccentStrongBrush", "#B7F7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
    }

    private static Button CreateRangeButton(string text, TimeRangePreset preset)
    {
        var button = new Button
        {
            Content = text,
            Tag = preset,
            Padding = new Thickness(10, 6, 10, 6),
            MinHeight = 32,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
        };

        button.Click += OnRangeClick;
        return button;
    }

    private static void OnRangeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TimeRangePreset preset } button &&
            button.Parent is FrameworkElement parent)
        {
            var card = FindParent<TelemetryPanelCard>(parent);
            if (card?.Panel is not null)
            {
                card.Panel.SelectedRange = preset;
            }
        }
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

    private static void ApplyRangeState(Button control, bool isActive)
    {
        control.Opacity = isActive ? 1 : 0.62;
        control.Background = isActive
            ? ResolveBrush("AccentSoftBrush", "#10394D")
            : ResolveBrush("SurfaceStrongBrush", "#183148");
        control.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        control.BorderBrush = isActive
            ? ResolveBrush("AccentStrongBrush", "#B7F7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
        control.BorderThickness = new Thickness(1);
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
        MetricUnit.Gigabytes => $"{value:0.0} GB",
        MetricUnit.MegabytesPerSecond => $"{value:0.0} MB/s",
        MetricUnit.MegabitsPerSecond => $"{value:0.0} Mbps",
        MetricUnit.Megahertz => $"{value / 1000d:0.00} GHz",
        _ => $"{value:0.##}",
    };
}
