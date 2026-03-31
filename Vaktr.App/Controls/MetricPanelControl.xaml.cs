using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public partial class MetricPanelControl : UserControl
{
    public static readonly DependencyProperty PanelProperty =
        DependencyProperty.Register(nameof(Panel), typeof(MetricPanelViewModel), typeof(MetricPanelControl), new PropertyMetadata(null, OnPanelChanged));

    public static readonly DependencyProperty HoverRatioProperty =
        DependencyProperty.Register(nameof(HoverRatio), typeof(double), typeof(MetricPanelControl), new PropertyMetadata(double.NaN));

    public static readonly DependencyProperty IsExpandedViewProperty =
        DependencyProperty.Register(nameof(IsExpandedView), typeof(bool), typeof(MetricPanelControl), new PropertyMetadata(false, OnExpandedModeChanged));

    public MetricPanelControl()
    {
        InitializeComponent();
    }

    public event EventHandler<PanelExpandRequestedEventArgs>? ExpandRequested;

    public MetricPanelViewModel? Panel
    {
        get => (MetricPanelViewModel?)GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    public double HoverRatio
    {
        get => (double)GetValue(HoverRatioProperty);
        set => SetValue(HoverRatioProperty, value);
    }

    public bool IsExpandedView
    {
        get => (bool)GetValue(IsExpandedViewProperty);
        set => SetValue(IsExpandedViewProperty, value);
    }

    private static void OnPanelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        var control = (MetricPanelControl)dependencyObject;
        control.RefreshRangeButtons();
    }

    private static void OnExpandedModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        var control = (MetricPanelControl)dependencyObject;
        control.ExpandButton.Visibility = control.IsExpandedView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PanelCard.Opacity = 0;
        var opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420));
        PanelCard.BeginAnimation(OpacityProperty, opacityAnimation);

        var translateAnimation = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(420))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        PanelTranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
        RefreshRangeButtons();
        ExpandButton.Visibility = IsExpandedView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPanelMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateHover(1.01, -4);
    }

    private void OnPanelMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateHover(1.0, 0);
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
        if (Panel is not null)
        {
            ExpandRequested?.Invoke(this, new PanelExpandRequestedEventArgs(Panel));
        }
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Panel is null)
        {
            return;
        }

        var position = e.GetPosition((IInputElement)sender);
        if (ActualWidth <= 0)
        {
            return;
        }

        HoverRatio = Math.Clamp(position.X / ((FrameworkElement)sender).ActualWidth, 0d, 1d);
        var hover = Panel.BuildHoverInfo(HoverRatio);
        if (hover is null)
        {
            HoverCard.Visibility = Visibility.Collapsed;
            return;
        }

        HoverCard.Visibility = Visibility.Visible;
        HoverTimeText.Text = hover.Timestamp.LocalDateTime.ToString("t");
        HoverValueText.Text = string.Join(Environment.NewLine, hover.Values.Select(value => $"{value.Label}: {value.Value}"));

        Canvas.SetLeft(HoverCard, Math.Min(position.X + 12, Math.Max(0, ((FrameworkElement)sender).ActualWidth - HoverCard.ActualWidth - 12)));
        Canvas.SetTop(HoverCard, 12);
    }

    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HoverRatio = double.NaN;
        HoverCard.Visibility = Visibility.Collapsed;
    }

    private void AnimateHover(double scale, double offset)
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        PanelScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        PanelScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        PanelTranslateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
    }

    private void RefreshRangeButtons()
    {
        ApplyRangeState(OneMinuteButton, Panel?.SelectedRange == TimeRangePreset.OneMinute);
        ApplyRangeState(FiveMinuteButton, Panel?.SelectedRange == TimeRangePreset.FiveMinutes);
        ApplyRangeState(FifteenMinuteButton, Panel?.SelectedRange == TimeRangePreset.FifteenMinutes);
        ApplyRangeState(OneHourButton, Panel?.SelectedRange == TimeRangePreset.OneHour);
    }

    private static void ApplyRangeState(Control control, bool? isActive)
    {
        control.Opacity = isActive == true ? 1 : 0.62;
    }
}

public sealed class PanelExpandRequestedEventArgs : EventArgs
{
    public PanelExpandRequestedEventArgs(MetricPanelViewModel panel)
    {
        Panel = panel;
    }

    public MetricPanelViewModel Panel { get; }
}
