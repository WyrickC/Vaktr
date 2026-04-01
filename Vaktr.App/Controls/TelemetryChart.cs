using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed class TelemetryChart : UserControl
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series),
            typeof(IReadOnlyList<ChartSeriesViewModel>),
            typeof(TelemetryChart),
            new PropertyMetadata(Array.Empty<ChartSeriesViewModel>(), OnChartPropertyChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(MetricUnit),
            typeof(TelemetryChart),
            new PropertyMetadata(MetricUnit.Percent, OnChartPropertyChanged));

    public static readonly DependencyProperty WindowStartUtcProperty =
        DependencyProperty.Register(
            nameof(WindowStartUtc),
            typeof(DateTimeOffset),
            typeof(TelemetryChart),
            new PropertyMetadata(default(DateTimeOffset), OnChartPropertyChanged));

    public static readonly DependencyProperty WindowEndUtcProperty =
        DependencyProperty.Register(
            nameof(WindowEndUtc),
            typeof(DateTimeOffset),
            typeof(TelemetryChart),
            new PropertyMetadata(default(DateTimeOffset), OnChartPropertyChanged));

    private readonly Canvas _canvas;
    private readonly TextBlock _emptyStateText;
    private bool _redrawQueued;

    public TelemetryChart()
    {
        UseLayoutRounding = true;

        _canvas = new Canvas();
        _emptyStateText = new TextBlock
        {
            Margin = new Thickness(10, 8, 0, 0),
            FontSize = 12,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = "Waiting for samples",
            Visibility = Visibility.Collapsed,
        };

        Content = new Grid
        {
            Children =
            {
                _canvas,
                _emptyStateText,
            },
        };

        Loaded += (_, _) => ScheduleRedraw();
        SizeChanged += (_, _) => ScheduleRedraw();
    }

    public IReadOnlyList<ChartSeriesViewModel> Series
    {
        get => (IReadOnlyList<ChartSeriesViewModel>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public MetricUnit Unit
    {
        get => (MetricUnit)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public DateTimeOffset WindowStartUtc
    {
        get => (DateTimeOffset)GetValue(WindowStartUtcProperty);
        set => SetValue(WindowStartUtcProperty, value);
    }

    public DateTimeOffset WindowEndUtc
    {
        get => (DateTimeOffset)GetValue(WindowEndUtcProperty);
        set => SetValue(WindowEndUtcProperty, value);
    }

    private static void OnChartPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((TelemetryChart)dependencyObject).ScheduleRedraw();
    }

    private void ScheduleRedraw()
    {
        if (_redrawQueued)
        {
            return;
        }

        _redrawQueued = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _redrawQueued = false;
            Redraw();
        });
    }

    private void Redraw()
    {
        var width = Math.Max(0, ActualWidth);
        var height = Math.Max(0, ActualHeight);
        if (width < 24 || height < 24)
        {
            return;
        }

        _canvas.Children.Clear();
        DrawGrid(width, height);

        var series = Series ?? Array.Empty<ChartSeriesViewModel>();
        var allPoints = series.SelectMany(item => item.Points).ToArray();
        if (allPoints.Length == 0)
        {
            _emptyStateText.Visibility = Visibility.Visible;
            return;
        }

        _emptyStateText.Visibility = Visibility.Collapsed;

        var start = WindowStartUtc == default ? allPoints.Min(point => point.Timestamp) : WindowStartUtc;
        var end = WindowEndUtc == default ? allPoints.Max(point => point.Timestamp) : WindowEndUtc;
        if (end <= start)
        {
            end = start.AddMinutes(1);
        }

        var maxValue = ResolveMaxValue(allPoints);
        foreach (var item in series)
        {
            if (item.Points.Count == 0)
            {
                continue;
            }

            var projected = Downsample(item.Points, Math.Max(48, (int)Math.Round(width * 1.35)))
                .Select(point => Project(point, width, height, start, end, maxValue))
                .ToArray();

            if (projected.Length == 0)
            {
                continue;
            }

            if (projected.Length == 1)
            {
                DrawPoint(projected[0], item.StrokeBrush, 5);
                continue;
            }

            _canvas.Children.Add(new Path
            {
                Data = BuildAreaGeometry(projected, height),
                Fill = item.FillBrush,
                Opacity = 0.7,
            });

            _canvas.Children.Add(new Path
            {
                Data = BuildLineGeometry(projected),
                Stroke = item.StrokeBrush,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeThickness = 2,
            });

            DrawPoint(projected[^1], item.StrokeBrush, 5);
        }
    }

    private void DrawGrid(double width, double height)
    {
        var gridBrush = ResolveBrush("SurfaceGridBrush", "#22405C");
        for (var index = 1; index <= 4; index++)
        {
            var y = (height / 5d) * index;
            _canvas.Children.Add(new Line
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                Opacity = index == 4 ? 0.18 : 0.12,
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
            });
        }
    }

    private double ResolveMaxValue(IReadOnlyList<MetricPoint> points)
    {
        if (Unit == MetricUnit.Percent)
        {
            return 100d;
        }

        var peak = points.Max(point => point.Value);
        return peak <= 0 ? 1d : peak * 1.14d;
    }

    private void DrawPoint(Windows.Foundation.Point point, Brush brush, double size)
    {
        var dot = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush,
        };

        Canvas.SetLeft(dot, point.X - (size / 2d));
        Canvas.SetTop(dot, point.Y - (size / 2d));
        _canvas.Children.Add(dot);
    }

    private static IReadOnlyList<MetricPoint> Downsample(IReadOnlyList<MetricPoint> points, int maxPoints)
    {
        if (points.Count <= maxPoints || maxPoints < 3)
        {
            return points;
        }

        var sampled = new List<MetricPoint>(maxPoints);
        var step = (points.Count - 1d) / (maxPoints - 1d);
        for (var index = 0; index < maxPoints; index++)
        {
            var sourceIndex = (int)Math.Round(index * step);
            sampled.Add(points[Math.Clamp(sourceIndex, 0, points.Count - 1)]);
        }

        return sampled;
    }

    private static Windows.Foundation.Point Project(
        MetricPoint point,
        double width,
        double height,
        DateTimeOffset start,
        DateTimeOffset end,
        double maxValue)
    {
        var totalMs = Math.Max(1d, (end - start).TotalMilliseconds);
        var xRatio = (point.Timestamp - start).TotalMilliseconds / totalMs;
        var yRatio = maxValue <= 0d ? 0d : point.Value / maxValue;

        return new Windows.Foundation.Point(
            Math.Clamp(xRatio, 0d, 1d) * width,
            height - (Math.Clamp(yRatio, 0d, 1d) * height));
    }

    private static Geometry BuildLineGeometry(IReadOnlyList<Windows.Foundation.Point> points)
    {
        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = false,
            IsFilled = false,
        };

        var segments = new PathSegmentCollection();
        for (var index = 1; index < points.Count; index++)
        {
            segments.Add(new LineSegment { Point = points[index] });
        }

        figure.Segments = segments;
        return new PathGeometry
        {
            Figures = new PathFigureCollection { figure },
        };
    }

    private static Geometry BuildAreaGeometry(IReadOnlyList<Windows.Foundation.Point> points, double height)
    {
        var figure = new PathFigure
        {
            StartPoint = new Windows.Foundation.Point(points[0].X, height),
            IsClosed = true,
            IsFilled = true,
        };

        var segments = new PathSegmentCollection
        {
            new LineSegment { Point = points[0] },
        };

        for (var index = 1; index < points.Count; index++)
        {
            segments.Add(new LineSegment { Point = points[index] });
        }

        segments.Add(new LineSegment { Point = new Windows.Foundation.Point(points[^1].X, height) });
        figure.Segments = segments;

        return new PathGeometry
        {
            Figures = new PathFigureCollection { figure },
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
}
