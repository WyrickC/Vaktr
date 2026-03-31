using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed class WinUiChartSurface : UserControl
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series),
            typeof(IReadOnlyList<ChartSeriesViewModel>),
            typeof(WinUiChartSurface),
            new PropertyMetadata(Array.Empty<ChartSeriesViewModel>(), OnChartPropertyChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(MetricUnit),
            typeof(WinUiChartSurface),
            new PropertyMetadata(MetricUnit.Percent, OnChartPropertyChanged));

    public static readonly DependencyProperty WindowStartUtcProperty =
        DependencyProperty.Register(
            nameof(WindowStartUtc),
            typeof(DateTimeOffset),
            typeof(WinUiChartSurface),
            new PropertyMetadata(default(DateTimeOffset), OnChartPropertyChanged));

    public static readonly DependencyProperty WindowEndUtcProperty =
        DependencyProperty.Register(
            nameof(WindowEndUtc),
            typeof(DateTimeOffset),
            typeof(WinUiChartSurface),
            new PropertyMetadata(default(DateTimeOffset), OnChartPropertyChanged));

    public static readonly DependencyProperty HoverRatioProperty =
        DependencyProperty.Register(
            nameof(HoverRatio),
            typeof(double),
            typeof(WinUiChartSurface),
            new PropertyMetadata(double.NaN, OnChartPropertyChanged));

    private readonly Canvas _canvas;
    private readonly TextBlock _emptyStateText;

    public WinUiChartSurface()
    {
        _canvas = new Canvas();
        _emptyStateText = new TextBlock
        {
            Margin = new Thickness(12, 8, 0, 0),
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

        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
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

    public double HoverRatio
    {
        get => (double)GetValue(HoverRatioProperty);
        set => SetValue(HoverRatioProperty, value);
    }

    private static void OnChartPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((WinUiChartSurface)dependencyObject).Redraw();
    }

    private void Redraw()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _canvas.Children.Clear();
        DrawGridLines(width, height);

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

        var maxValue = Unit == MetricUnit.Percent ? 100d : Math.Max(1d, allPoints.Max(point => point.Value) * 1.12d);

        foreach (var item in series)
        {
            if (item.Points.Count == 0)
            {
                continue;
            }

            var screenPoints = item.Points
                .Select(point => Project(point, width, height, start, end, 0d, maxValue))
                .ToArray();

            if (screenPoints.Length == 1)
            {
                DrawPoint(screenPoints[0], item.StrokeBrush);
                continue;
            }

            _canvas.Children.Add(new Path
            {
                Data = BuildAreaGeometry(screenPoints, height),
                Fill = item.FillBrush,
            });

            _canvas.Children.Add(new Path
            {
                Data = BuildLineGeometry(screenPoints),
                Stroke = item.StrokeBrush,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeThickness = 2.2,
            });

            var latest = screenPoints[^1];
            DrawPoint(latest, new SolidColorBrush(ColorHelper.FromArgb(26, 255, 255, 255)), 12);
            DrawPoint(latest, item.StrokeBrush, 4.5);
        }

        if (!double.IsNaN(HoverRatio))
        {
            var x = Math.Clamp(HoverRatio, 0d, 1d) * width;
            _canvas.Children.Add(new Line
            {
                Stroke = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
                StrokeThickness = 1,
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
            });
        }
    }

    private void DrawGridLines(double width, double height)
    {
        var gridBrush = ResolveBrush("SurfaceGridBrush", "#22405C");
        for (var index = 1; index <= 4; index++)
        {
            var y = (height / 5d) * index;
            _canvas.Children.Add(new Line
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
            });
        }
    }

    private void DrawPoint(Windows.Foundation.Point point, Brush brush, double size = 9)
    {
        var ellipse = new Ellipse
        {
            Fill = brush,
            Width = size,
            Height = size,
        };

        Canvas.SetLeft(ellipse, point.X - (size / 2d));
        Canvas.SetTop(ellipse, point.Y - (size / 2d));
        _canvas.Children.Add(ellipse);
    }

    private static Windows.Foundation.Point Project(
        MetricPoint point,
        double width,
        double height,
        DateTimeOffset start,
        DateTimeOffset end,
        double minValue,
        double maxValue)
    {
        var totalMs = Math.Max(1d, (end - start).TotalMilliseconds);
        var xRatio = (point.Timestamp - start).TotalMilliseconds / totalMs;
        var yRatio = maxValue <= minValue ? 0d : (point.Value - minValue) / (maxValue - minValue);

        return new Windows.Foundation.Point(
            Math.Clamp(xRatio, 0d, 1d) * width,
            height - (Math.Clamp(yRatio, 0d, 1d) * height));
    }

    private static Geometry BuildLineGeometry(IReadOnlyList<Windows.Foundation.Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
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
