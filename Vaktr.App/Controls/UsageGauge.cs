using Microsoft.UI.Dispatching;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

public sealed class UsageGauge : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(UsageGauge),
            new PropertyMetadata(0d, OnGaugePropertyChanged));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Brush),
            typeof(UsageGauge),
            new PropertyMetadata(null, OnGaugePropertyChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(
            nameof(Caption),
            typeof(string),
            typeof(UsageGauge),
            new PropertyMetadata("Usage", OnGaugePropertyChanged));

    private readonly Canvas _canvas;
    private readonly Border _frameBorder;
    private readonly Border _innerBorder;
    private readonly TextBlock _valueText;
    private readonly TextBlock _captionText;
    private bool _redrawQueued;
    private bool _redrawUpgradePending;
    private DispatcherQueuePriority _queuedRedrawPriority = DispatcherQueuePriority.Low;

    public UsageGauge()
    {
        _canvas = new Canvas();
        _frameBorder = new Border
        {
            CornerRadius = new CornerRadius(24),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            Opacity = 0.82,
        };
        _innerBorder = new Border
        {
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(20),
            BorderBrush = ResolveBrush("SurfaceGridBrush", "#35587A"),
            BorderThickness = new Thickness(1),
            Opacity = 0.18,
        };
        _valueText = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        };
        _captionText = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
        };

        Content = new Grid
        {
            Children =
            {
                _frameBorder,
                _innerBorder,
                _canvas,
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        _valueText,
                        _captionText,
                    },
                },
            },
        };

        Loaded += (_, _) => ScheduleRedraw();
        SizeChanged += (_, _) => ScheduleRedraw();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private static void OnGaugePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((UsageGauge)dependencyObject).ScheduleRedraw();
    }

    public void RefreshThemeResources()
    {
        _frameBorder.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _frameBorder.Background = ResolveBrush("SurfaceBrush", "#102131");
        _innerBorder.BorderBrush = ResolveBrush("SurfaceGridBrush", "#35587A");
        _valueText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _captionText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        ScheduleRedraw(DispatcherQueuePriority.High);
    }

    private void ScheduleRedraw(DispatcherQueuePriority priority = DispatcherQueuePriority.Low)
    {
        if (_redrawQueued)
        {
            if (GetPriorityRank(priority) > GetPriorityRank(_queuedRedrawPriority))
            {
                _queuedRedrawPriority = priority;
                _redrawUpgradePending = true;
            }

            return;
        }

        _redrawQueued = true;
        _queuedRedrawPriority = priority;
        _ = DispatcherQueue.TryEnqueue(priority, () =>
        {
            _redrawQueued = false;
            Redraw();

            if (_redrawUpgradePending)
            {
                _redrawUpgradePending = false;
                ScheduleRedraw(_queuedRedrawPriority);
            }
        });
    }

    private static int GetPriorityRank(DispatcherQueuePriority priority) => priority switch
    {
        DispatcherQueuePriority.High => 2,
        DispatcherQueuePriority.Normal => 1,
        _ => 0,
    };

    private void Redraw()
    {
        var width = Math.Max(120, ActualWidth);
        var height = Math.Max(120, ActualHeight);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var centerX = width / 2d;
        var centerY = height / 2d;
        var radius = Math.Max(18, Math.Min(width, height) / 2d - 12);
        var startAngle = 135d;
        var totalSweep = 270d;
        var sweep = Math.Clamp(Value, 0d, 100d) / 100d * totalSweep;

        _canvas.Children.Clear();
        _canvas.Children.Add(CreateArcPath(
            centerX,
            centerY,
            radius,
            startAngle,
            totalSweep,
            ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            11,
            0.32));

        _canvas.Children.Add(CreateArcPath(
            centerX,
            centerY,
            radius,
            startAngle,
            sweep,
            AccentBrush ?? ResolveBrush("AccentBrush", "#66E7FF"),
            11,
            1));

        _valueText.Text = $"{Math.Clamp(Value, 0d, 100d):0.#}%";
        _captionText.Text = Caption;
    }

    private static Path CreateArcPath(
        double centerX,
        double centerY,
        double radius,
        double startAngle,
        double sweepAngle,
        Brush stroke,
        double thickness,
        double opacity)
    {
        var start = ToPoint(centerX, centerY, radius, startAngle);
        var end = ToPoint(centerX, centerY, radius, startAngle + sweepAngle);
        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false,
            Segments = new PathSegmentCollection
            {
                new ArcSegment
                {
                    Point = end,
                    Size = new Windows.Foundation.Size(radius, radius),
                    IsLargeArc = sweepAngle > 180,
                    SweepDirection = SweepDirection.Clockwise,
                },
            },
        };

        return new Path
        {
            Data = new PathGeometry
            {
                Figures = new PathFigureCollection { figure },
            },
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = opacity,
        };
    }

    private static Windows.Foundation.Point ToPoint(double centerX, double centerY, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Windows.Foundation.Point(
            centerX + Math.Cos(radians) * radius,
            centerY + Math.Sin(radians) * radius);
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
    }
}
