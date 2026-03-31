using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed class MetricDeck : UserControl
{
    public static readonly DependencyProperty PanelProperty =
        DependencyProperty.Register(
            nameof(Panel),
            typeof(MetricPanelViewModel),
            typeof(MetricDeck),
            new PropertyMetadata(null, OnPanelChanged));

    public static readonly DependencyProperty IsExpandedViewProperty =
        DependencyProperty.Register(
            nameof(IsExpandedView),
            typeof(bool),
            typeof(MetricDeck),
            new PropertyMetadata(false, OnExpandedModeChanged));

    private readonly Border _panelCard;
    private readonly Border _hoverGlow;
    private readonly TextBlock _badgeText;
    private readonly TextBlock _footerText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _currentValueText;
    private readonly TextBlock _secondaryValueText;
    private readonly Button _oneMinuteButton;
    private readonly Button _fiveMinuteButton;
    private readonly Button _fifteenMinuteButton;
    private readonly Button _oneHourButton;
    private readonly Button _expandButton;
    private readonly StackPanel _seriesHost;

    private MetricPanelViewModel? _observedPanel;

    public MetricDeck()
    {
        Width = 430;
        MinHeight = 280;

        _badgeText = CreateTextBlock("Bahnschrift", 11, FontWeights.SemiBold);
        _footerText = CreateTextBlock(fontSize: 11);
        _titleText = CreateTextBlock("Segoe UI Variable Display", 24, FontWeights.SemiBold);
        _currentValueText = CreateTextBlock("Bahnschrift", 30, FontWeights.SemiBold);
        _secondaryValueText = CreateTextBlock(fontSize: 13);

        _oneMinuteButton = CreateRangeButton("1m", "OneMinute");
        _fiveMinuteButton = CreateRangeButton("5m", "FiveMinutes");
        _fifteenMinuteButton = CreateRangeButton("15m", "FifteenMinutes");
        _oneHourButton = CreateRangeButton("1h", "OneHour");
        _oneMinuteButton.Click += OnRangeClick;
        _fiveMinuteButton.Click += OnRangeClick;
        _fifteenMinuteButton.Click += OnRangeClick;
        _oneHourButton.Click += OnRangeClick;
        _expandButton = CreateGhostButton("Expand");
        _expandButton.Click += OnExpandClick;

        _seriesHost = new StackPanel
        {
            Spacing = 8,
        };

        var badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4, 10, 4),
            Child = _badgeText,
        };

        var headerText = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        badgeBorder,
                        _footerText,
                    },
                },
                _titleText,
                _currentValueText,
                _secondaryValueText,
            },
        };

        var actionsHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 8,
            Children =
            {
                _oneMinuteButton,
                _fiveMinuteButton,
                _fifteenMinuteButton,
                _oneHourButton,
                _expandButton,
            },
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(headerText);
        headerGrid.Children.Add(actionsHost);
        Grid.SetColumn(actionsHost, 1);

        var bodyGrid = new Grid();
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bodyGrid.Children.Add(headerGrid);
        var seriesSurface = new Border
        {
            Margin = new Thickness(0, 18, 0, 0),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(18),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Auto,
                Content = _seriesHost,
            },
        };
        bodyGrid.Children.Add(seriesSurface);
        Grid.SetRow(seriesSurface, 1);

        _hoverGlow = new Border
        {
            Opacity = 0,
            CornerRadius = new CornerRadius(24),
        };

        _panelCard = new Border
        {
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(20),
            Child = new Grid
            {
                Children =
                {
                    _hoverGlow,
                    new Border
                    {
                        CornerRadius = new CornerRadius(24),
                        Opacity = 0.48,
                    },
                    bodyGrid,
                },
            },
        };
        _panelCard.PointerEntered += OnPanelPointerEntered;
        _panelCard.PointerExited += OnPanelPointerExited;

        Content = _panelCard;
        Loaded += (_, _) =>
        {
            RefreshVisuals();
            RefreshFromPanel();
        };
    }

    public MetricPanelViewModel? Panel
    {
        get => (MetricPanelViewModel?)GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    public bool IsExpandedView
    {
        get => (bool)GetValue(IsExpandedViewProperty);
        set => SetValue(IsExpandedViewProperty, value);
    }

    private static void OnPanelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (MetricDeck)dependencyObject;
        control.DetachPanel(args.OldValue as MetricPanelViewModel);
        control.AttachPanel(args.NewValue as MetricPanelViewModel);
        control.RefreshFromPanel();
    }

    private static void OnExpandedModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (MetricDeck)dependencyObject;
        control._expandButton.Visibility = control.IsExpandedView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AttachPanel(MetricPanelViewModel? panel)
    {
        _observedPanel = panel;
        if (_observedPanel is null)
        {
            return;
        }

        _observedPanel.PropertyChanged += OnPanelPropertyChanged;
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
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            RefreshFromPanel();
        });
    }

    private void RefreshFromPanel()
    {
        var panel = Panel;
        _expandButton.Visibility = IsExpandedView ? Visibility.Collapsed : Visibility.Visible;

        if (panel is null)
        {
            _badgeText.Text = "LIVE";
            _footerText.Text = string.Empty;
            _titleText.Text = "Telemetry";
            _currentValueText.Text = "Waiting";
            _secondaryValueText.Text = "Collecting samples";
            _seriesHost.Children.Clear();
            _seriesHost.Children.Add(CreateEmptySeriesText("Waiting for samples"));
            return;
        }

        _badgeText.Text = panel.Badge;
        _footerText.Text = panel.FooterText;
        _titleText.Text = panel.Title;
        _currentValueText.Text = panel.CurrentValue;
        _secondaryValueText.Text = panel.SecondaryValue;
        RefreshSeriesRows(panel);
        RefreshRangeButtons();
        RefreshVisuals();
    }

    private void RefreshSeriesRows(MetricPanelViewModel panel)
    {
        _seriesHost.Children.Clear();
        if (panel.VisibleSeries.Count == 0)
        {
            _seriesHost.Children.Add(CreateEmptySeriesText("Waiting for samples"));
            return;
        }

        foreach (var series in panel.VisibleSeries)
        {
            var latestValue = series.Points.Count == 0
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
                Margin = new Thickness(0, 5, 12, 0),
                Fill = series.StrokeBrush,
            });

            var nameText = new TextBlock
            {
                FontSize = 13,
                Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
                Text = series.Name,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.Children.Add(nameText);
            Grid.SetColumn(nameText, 1);

            var valueText = new TextBlock
            {
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
                Text = latestValue,
            };
            row.Children.Add(valueText);
            Grid.SetColumn(valueText, 2);

            _seriesHost.Children.Add(new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(14),
                Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            FontSize = 11,
                            CharacterSpacing = 50,
                            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
                            Text = $"{series.Points.Count} SAMPLES",
                        },
                        row,
                    },
                },
            });
        }
    }

    private void RefreshRangeButtons()
    {
        ApplyRangeState(_oneMinuteButton, Panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(_fiveMinuteButton, Panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(_fifteenMinuteButton, Panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(_oneHourButton, Panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private void RefreshVisuals()
    {
        var surfaceBrush = ResolveBrush("SurfaceBrush", "#102131");
        var panelOverlay = ResolveBrush("PanelOverlayBrush", "#11283C");
        var strokeBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        var textPrimary = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        var textSecondary = ResolveBrush("TextSecondaryBrush", "#B7CCE1");
        var textMuted = ResolveBrush("TextMutedBrush", "#7D9AB6");
        var accentStrong = ResolveBrush("AccentStrongBrush", "#B7F7FF");
        var accentSoft = ResolveBrush("AccentSoftBrush", "#10394D");
        var accentHalo = ResolveBrush("AccentHaloBrush", "#1B68DAFF");

        _panelCard.Background = surfaceBrush;
        _panelCard.BorderBrush = strokeBrush;
        _panelCard.BorderThickness = new Thickness(1);
        _hoverGlow.Background = accentHalo;

        if (_panelCard.Child is Grid rootGrid && rootGrid.Children.Count > 1 && rootGrid.Children[1] is Border overlay)
        {
            overlay.Background = panelOverlay;
        }

        _badgeText.Foreground = accentStrong;
        _footerText.Foreground = textMuted;
        _titleText.Foreground = textPrimary;
        _currentValueText.Foreground = textPrimary;
        _secondaryValueText.Foreground = textSecondary;

        if (_badgeText.Parent is Border badgeBorder)
        {
            badgeBorder.Background = accentSoft;
        }

        ApplyRangeState(_oneMinuteButton, Panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(_fiveMinuteButton, Panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(_fifteenMinuteButton, Panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(_oneHourButton, Panel?.SelectedRange == TimeRangePreset.OneHour);
        ApplyGhostButtonVisual(_expandButton);
    }

    private void OnPanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hoverGlow.Opacity = 1;
        _panelCard.BorderBrush = ResolveBrush("AccentStrongBrush", "#B7F7FF");
    }

    private void OnPanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverGlow.Opacity = 0;
        _panelCard.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
    }

    private void OnRangeClick(object sender, RoutedEventArgs e)
    {
        if (Panel is null || sender is not Button button)
        {
            return;
        }

        Panel.SelectedRange = button.Tag?.ToString() switch
        {
            "OneMinute" => TimeRangePreset.OneMinute,
            "FiveMinutes" => TimeRangePreset.FiveMinutes,
            "FifteenMinutes" => TimeRangePreset.FifteenMinutes,
            "OneHour" => TimeRangePreset.OneHour,
            _ => Panel.SelectedRange,
        };

        RefreshRangeButtons();
    }

    private void OnExpandClick(object sender, RoutedEventArgs e) => Panel?.RequestExpand();

    private void ApplyRangeState(Button control, bool? isActive)
    {
        control.Opacity = isActive == true ? 1 : 0.58;
        control.Background = isActive == true
            ? ResolveBrush("AccentSoftBrush", "#10394D")
            : ResolveBrush("SurfaceStrongBrush", "#183148");
        control.BorderBrush = isActive == true
            ? ResolveBrush("AccentStrongBrush", "#B7F7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");
    }

    private void ApplyGhostButtonVisual(Button button)
    {
        button.Background = ResolveBrush("SurfaceElevatedBrush", "#15283B");
        button.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        button.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        button.BorderThickness = new Thickness(1);
    }

    private static Button CreateRangeButton(string text, string tag)
    {
        var button = CreateGhostButton(text);
        button.Tag = tag;
        button.Padding = new Thickness(10, 6, 10, 6);
        button.MinHeight = 32;
        button.FontFamily = new FontFamily("Bahnschrift");
        button.FontSize = 12;
        return button;
    }

    private static Button CreateGhostButton(string text) =>
        new()
        {
            Content = text,
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 42,
        };

    private static TextBlock CreateTextBlock(string? fontFamily = null, double fontSize = 12, Windows.UI.Text.FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? new FontFamily("Segoe UI") : new FontFamily(fontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
    }

    private static TextBlock CreateEmptySeriesText(string text) => new()
    {
        FontSize = 13,
        Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
        Text = text,
        TextWrapping = TextWrapping.WrapWholeWords,
    };

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
