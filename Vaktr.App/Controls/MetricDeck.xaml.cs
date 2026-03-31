using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed partial class MetricDeck : UserControl
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

    public MetricDeck()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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
        ((MetricDeck)dependencyObject).RefreshRangeButtons();
    }

    private static void OnExpandedModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (MetricDeck)dependencyObject;
        if (!control.AreControlsReady())
        {
            return;
        }

        control.ExpandButton.Visibility = control.IsExpandedView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshRangeButtons();
        ExpandButton.Visibility = IsExpandedView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        HoverGlow.Opacity = 1;
        PanelCard.BorderBrush = ResolveBrush("AccentStrongBrush");
    }

    private void OnPanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        HoverGlow.Opacity = 0;
        PanelCard.BorderBrush = ResolveBrush("SurfaceStrokeBrush");
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

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        Panel?.RequestExpand();
    }

    private void OnChartPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Panel is null || sender is not FrameworkElement area || area.ActualWidth <= 0)
        {
            return;
        }

        var position = e.GetCurrentPoint(area).Position;
        var ratio = Math.Clamp(position.X / area.ActualWidth, 0d, 1d);
        ChartSurface.HoverRatio = ratio;

        var hover = Panel.BuildHoverInfo(ratio);
        if (hover is null)
        {
            HoverCard.Visibility = Visibility.Collapsed;
            return;
        }

        HoverCard.Visibility = Visibility.Visible;
        HoverTimeText.Text = hover.Timestamp.LocalDateTime.ToString("t");
        HoverValueText.Text = string.Join(Environment.NewLine, hover.Values.Select(value => $"{value.Label}: {value.Value}"));

        HoverCard.UpdateLayout();
        Canvas.SetLeft(HoverCard, Math.Min(position.X + 16, Math.Max(0, area.ActualWidth - HoverCard.ActualWidth - 16)));
        Canvas.SetTop(HoverCard, 12);
    }

    private void OnChartPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ChartSurface.HoverRatio = double.NaN;
        HoverCard.Visibility = Visibility.Collapsed;
    }

    private void RefreshRangeButtons()
    {
        if (!AreControlsReady())
        {
            return;
        }

        ApplyRangeState(OneMinuteButton, Panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(FiveMinuteButton, Panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(FifteenMinuteButton, Panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(OneHourButton, Panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private bool AreControlsReady() =>
        OneMinuteButton is not null &&
        FiveMinuteButton is not null &&
        FifteenMinuteButton is not null &&
        OneHourButton is not null &&
        ExpandButton is not null;

    private void ApplyRangeState(Button control, bool? isActive)
    {
        control.Opacity = isActive == true ? 1 : 0.58;
        control.Background = isActive == true ? ResolveBrush("AccentSoftBrush") : ResolveBrush("SurfaceStrongBrush");
        control.BorderBrush = isActive == true ? ResolveBrush("AccentStrongBrush") : ResolveBrush("SurfaceStrokeBrush");
    }

    private static Brush ResolveBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush("#FFFFFF");
    }
}
