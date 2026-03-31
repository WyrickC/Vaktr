using System.Windows;
using System.Windows.Media;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App.Controls;

public sealed class LineChartSurface : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series),
            typeof(IReadOnlyList<ChartSeriesViewModel>),
            typeof(LineChartSurface),
            new FrameworkPropertyMetadata(Array.Empty<ChartSeriesViewModel>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(MetricUnit),
            typeof(LineChartSurface),
            new FrameworkPropertyMetadata(MetricUnit.Percent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowStartUtcProperty =
        DependencyProperty.Register(
            nameof(WindowStartUtc),
            typeof(DateTimeOffset),
            typeof(LineChartSurface),
            new FrameworkPropertyMetadata(default(DateTimeOffset), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowEndUtcProperty =
        DependencyProperty.Register(
            nameof(WindowEndUtc),
            typeof(DateTimeOffset),
            typeof(LineChartSurface),
            new FrameworkPropertyMetadata(default(DateTimeOffset), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverRatioProperty =
        DependencyProperty.Register(
            nameof(HoverRatio),
            typeof(double),
            typeof(LineChartSurface),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

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

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        DrawGrid(drawingContext, rect);

        var allPoints = Series.SelectMany(series => series.Points).ToArray();
        if (allPoints.Length == 0)
        {
            DrawEmptyState(drawingContext, rect);
            return;
        }

        var start = WindowStartUtc == default ? allPoints.Min(point => point.Timestamp) : WindowStartUtc;
        var end = WindowEndUtc == default ? allPoints.Max(point => point.Timestamp) : WindowEndUtc;
        if (end <= start)
        {
            end = start.AddMinutes(1);
        }

        var maxValue = Unit == MetricUnit.Percent ? 100d : Math.Max(1d, allPoints.Max(point => point.Value) * 1.12d);
        var minValue = 0d;

        foreach (var series in Series)
        {
            if (series.Points.Count == 0)
            {
                continue;
            }

            var screenPoints = series.Points
                .Select(point => Project(point, rect, start, end, minValue, maxValue))
                .ToArray();

            if (screenPoints.Length == 1)
            {
                drawingContext.DrawEllipse(series.StrokeBrush, null, screenPoints[0], 4, 4);
                continue;
            }

            var lineGeometry = BuildSmoothLine(screenPoints);
            var areaGeometry = BuildAreaGeometry(screenPoints, rect.Bottom);
            drawingContext.DrawGeometry(series.FillBrush, null, areaGeometry);
            drawingContext.DrawGeometry(null, new Pen(series.StrokeBrush, 2.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, lineGeometry);

            var latest = screenPoints[^1];
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)), null, latest, 12, 12);
            drawingContext.DrawEllipse(series.StrokeBrush, null, latest, 4.5, 4.5);
        }

        if (!double.IsNaN(HoverRatio))
        {
            var x = rect.Left + (Math.Clamp(HoverRatio, 0d, 1d) * rect.Width);
            var hoverPen = new Pen((Brush)TryFindResource("AccentStrongBrush") ?? Brushes.LightBlue, 1);
            drawingContext.DrawLine(hoverPen, new Point(x, rect.Top), new Point(x, rect.Bottom));
        }
    }

    private void DrawGrid(DrawingContext drawingContext, Rect rect)
    {
        var gridBrush = (Brush?)TryFindResource("SurfaceGridBrush") ?? new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
        var pen = new Pen(gridBrush, 1);
        for (var index = 1; index <= 4; index++)
        {
            var y = rect.Top + ((rect.Height / 5d) * index);
            drawingContext.DrawLine(pen, new Point(rect.Left, y), new Point(rect.Right, y));
        }
    }

    private void DrawEmptyState(DrawingContext drawingContext, Rect rect)
    {
        var foreground = (Brush?)TryFindResource("TextMutedBrush") ?? Brushes.Gray;
        var text = new FormattedText(
            "Waiting for samples",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirectionMode.LeftToRight,
            new Typeface("Segoe UI Variable Text"),
            12,
            foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        drawingContext.DrawText(text, new Point(rect.Left + 12, rect.Top + 12));
    }

    private static Point Project(
        MetricPoint point,
        Rect rect,
        DateTimeOffset start,
        DateTimeOffset end,
        double minValue,
        double maxValue)
    {
        var totalMs = Math.Max(1d, (end - start).TotalMilliseconds);
        var xRatio = (point.Timestamp - start).TotalMilliseconds / totalMs;
        var yRatio = maxValue <= minValue ? 0d : (point.Value - minValue) / (maxValue - minValue);

        var x = rect.Left + (Math.Clamp(xRatio, 0d, 1d) * rect.Width);
        var y = rect.Bottom - (Math.Clamp(yRatio, 0d, 1d) * rect.Height);
        return new Point(x, y);
    }

    private static Geometry BuildSmoothLine(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], false, false);

        if (points.Count == 2)
        {
            context.LineTo(points[1], true, true);
        }
        else
        {
            for (var index = 0; index < points.Count - 1; index++)
            {
                var current = points[index];
                var next = points[index + 1];
                var midpoint = new Point((current.X + next.X) / 2d, (current.Y + next.Y) / 2d);
                context.QuadraticBezierTo(current, midpoint, true, true);
            }

            context.LineTo(points[^1], true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildAreaGeometry(IReadOnlyList<Point> points, double bottom)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].X, bottom), true, true);
        context.LineTo(points[0], true, true);

        if (points.Count == 2)
        {
            context.LineTo(points[1], true, true);
        }
        else
        {
            for (var index = 0; index < points.Count - 1; index++)
            {
                var current = points[index];
                var next = points[index + 1];
                var midpoint = new Point((current.X + next.X) / 2d, (current.Y + next.Y) / 2d);
                context.QuadraticBezierTo(current, midpoint, true, true);
            }

            context.LineTo(points[^1], true, true);
        }

        context.LineTo(new Point(points[^1].X, bottom), true, true);
        geometry.Freeze();
        return geometry;
    }
}
