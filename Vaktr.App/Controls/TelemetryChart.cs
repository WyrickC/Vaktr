using Vaktr.App.ViewModels;
using Vaktr.Core.Models;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.Text;

namespace Vaktr.App.Controls;

public sealed class TelemetryChart : UserControl
{
    private const double LeftPadding = 14d;
    private const double RightPadding = 14d;
    private const double TopPadding = 14d;
    private const double BottomPadding = 28d;
    private const double MinimumSelectionWidth = 18d;

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

    public static readonly DependencyProperty CeilingValueProperty =
        DependencyProperty.Register(
            nameof(CeilingValue),
            typeof(double),
            typeof(TelemetryChart),
            new PropertyMetadata(0d, OnChartPropertyChanged));

    public static readonly DependencyProperty EmptyStateTextProperty =
        DependencyProperty.Register(
            nameof(EmptyStateText),
            typeof(string),
            typeof(TelemetryChart),
            new PropertyMetadata("Waiting for samples", OnChartPropertyChanged));

    private readonly Canvas _canvas;
    private readonly Canvas _interactionCanvas;
    private readonly Rectangle _selectionRectangle;
    private readonly Line _hoverLine;
    private readonly Border _hoverTooltip;
    private readonly TextBlock _hoverTooltipText;
    private readonly TextBlock _emptyStateText;
    private bool _isSelecting;
    private bool _redrawQueued;
    private bool _redrawUpgradePending;
    private DispatcherQueuePriority _queuedRedrawPriority = DispatcherQueuePriority.Low;
    private double _selectionStartX;
    private double _selectionCurrentX;

    public TelemetryChart()
    {
        UseLayoutRounding = true;

        _canvas = new Canvas();
        _interactionCanvas = new Canvas();
        _selectionRectangle = new Rectangle
        {
            Visibility = Visibility.Collapsed,
            Stroke = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
            StrokeThickness = 1,
            Fill = BrushFactory.CreateBrush("#163BB7FF"),
            RadiusX = 8,
            RadiusY = 8,
            IsHitTestVisible = false,
        };
        _hoverLine = new Line
        {
            Visibility = Visibility.Collapsed,
            Stroke = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
            StrokeThickness = 1,
            Opacity = 0.32,
            IsHitTestVisible = false,
        };
        _hoverTooltipText = new TextBlock
        {
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            MaxWidth = 210,
        };
        _hoverTooltip = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = CreateSurfaceGradient("#15283B", "#203851"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(10, 8, 10, 8),
            Child = _hoverTooltipText,
            IsHitTestVisible = false,
        };
        _interactionCanvas.Children.Add(_selectionRectangle);
        _interactionCanvas.Children.Add(_hoverLine);
        _interactionCanvas.Children.Add(_hoverTooltip);
        _emptyStateText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = "Waiting for samples",
            TextAlignment = TextAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        Content = new Grid
        {
            Children =
            {
                _canvas,
                _interactionCanvas,
                _emptyStateText,
            },
        };

        Loaded += (_, _) => ScheduleRedraw();
        SizeChanged += (_, _) => ScheduleRedraw();
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerExited += (_, _) => HideHover();
        DoubleTapped += (_, _) => ZoomResetRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<ChartZoomSelectionEventArgs>? ZoomSelectionRequested;

    public event EventHandler? ZoomResetRequested;

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

    public double CeilingValue
    {
        get => (double)GetValue(CeilingValueProperty);
        set => SetValue(CeilingValueProperty, value);
    }

    public string EmptyStateText
    {
        get => (string)GetValue(EmptyStateTextProperty);
        set => SetValue(EmptyStateTextProperty, value);
    }

    private static void OnChartPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((TelemetryChart)dependencyObject).ScheduleRedraw();
    }

    public void RefreshThemeResources()
    {
        _hoverLine.Stroke = ResolveBrush("AccentStrongBrush", "#B7F7FF");
        _hoverTooltip.Background = CreateSurfaceGradient("#15283B", "#203851");
        _hoverTooltip.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
        _hoverTooltipText.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _emptyStateText.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
        _emptyStateText.Text = string.IsNullOrWhiteSpace(EmptyStateText) ? "Waiting for samples" : EmptyStateText;
        HideHover();
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
        var width = Math.Max(0, ActualWidth);
        var height = Math.Max(0, ActualHeight);
        if (width < 24 || height < 24)
        {
            return;
        }

        var series = Series ?? Array.Empty<ChartSeriesViewModel>();
        if (!TryGetPointBounds(series, out var minTimestamp, out var maxTimestamp, out var maxObservedValue))
        {
            _canvas.Children.Clear();
            var emptyPlotWidth = Math.Max(16d, width - LeftPadding - RightPadding);
            var emptyPlotHeight = Math.Max(16d, height - TopPadding - BottomPadding);
            DrawPlotSurface(emptyPlotWidth, emptyPlotHeight);
            DrawGrid(width, height, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, ResolveMaxValue(0d), includeLabels: false);
            _emptyStateText.Text = string.IsNullOrWhiteSpace(EmptyStateText) ? "Waiting for samples" : EmptyStateText;
            _emptyStateText.Visibility = Visibility.Visible;
            HideHover();
            return;
        }

        _emptyStateText.Visibility = Visibility.Collapsed;

        var start = WindowStartUtc == default ? minTimestamp : WindowStartUtc;
        var end = WindowEndUtc == default ? maxTimestamp : WindowEndUtc;
        if (end <= start)
        {
            end = start.AddMinutes(1);
        }

        var maxValue = ResolveMaxValue(maxObservedValue);
        var plotWidth = Math.Max(16d, width - LeftPadding - RightPadding);
        var plotHeight = Math.Max(16d, height - TopPadding - BottomPadding);
        var seriesCount = series.Count;
        var drawFilledArea = seriesCount <= 3;
        var drawPointMarkers = seriesCount <= 4;
        var pointBudget = ResolvePointBudget(width, seriesCount, end - start);
        var strokeThickness = seriesCount <= 2 ? 2d : seriesCount <= 6 ? 1.7d : 1.35d;

        _canvas.Children.Clear();
        DrawPlotSurface(plotWidth, plotHeight);
        DrawGrid(width, height, start, end, maxValue);
        foreach (var item in series)
        {
            if (item.Points.Count == 0)
            {
                continue;
            }

            var renderablePoints = GetRenderablePoints(item.Points, start, end);
            if (renderablePoints.Count == 0)
            {
                continue;
            }

            var downsampled = Downsample(renderablePoints, pointBudget);

            // Split into contiguous segments at gaps (>3x the expected interval between points)
            var segments = SplitAtGaps(downsampled, end - start);

            foreach (var segment in segments)
            {
                var projected = NormalizeProjectedPoints(
                        segment.Select(point => Project(point, LeftPadding, TopPadding, plotWidth, plotHeight, start, end, maxValue)))
                    .ToArray();
                var strokeProjected = ResolveStrokePoints(projected);

                if (strokeProjected.Count == 0)
                {
                    continue;
                }

                if (strokeProjected.Count == 1)
                {
                    DrawPoint(strokeProjected[0], item.StrokeBrush, 5);
                    continue;
                }

                if (drawFilledArea)
                {
                    var fillProjected = ResolveFillPoints(strokeProjected);
                    if (fillProjected.Count >= 2)
                    {
                        _canvas.Children.Add(new Path
                        {
                            Data = BuildAreaGeometry(fillProjected, TopPadding + plotHeight),
                            Fill = item.FillBrush,
                            Opacity = seriesCount == 1 ? 0.7 : 0.5,
                        });
                    }
                }

                _canvas.Children.Add(new Path
                {
                    Data = BuildLineGeometry(strokeProjected),
                    Stroke = item.StrokeBrush,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeThickness = strokeThickness,
                });
            }

            if (drawPointMarkers && renderablePoints.Count > 0)
            {
                var lastPoint = Project(renderablePoints[^1], LeftPadding, TopPadding, plotWidth, plotHeight, start, end, maxValue);
                DrawPoint(lastPoint, item.StrokeBrush, 5);
            }
        }
    }

    private void DrawPlotSurface(double plotWidth, double plotHeight)
    {
        var frame = new Rectangle
        {
            Width = plotWidth,
            Height = plotHeight,
            RadiusX = 16,
            RadiusY = 16,
            Stroke = ResolveBrush("SurfaceGridBrush", "#27425E"),
            StrokeThickness = 1,
            Fill = CreateSurfaceGradient("#0B1726", "#12243A"),
            Opacity = 0.98,
        };

        var tint = new Rectangle
        {
            Width = plotWidth,
            Height = plotHeight,
            RadiusX = 16,
            RadiusY = 16,
            Fill = ResolveBrush("AccentSoftBrush", "#10394D"),
            Opacity = 0.06,
        };

        Canvas.SetLeft(frame, LeftPadding);
        Canvas.SetTop(frame, TopPadding);
        _canvas.Children.Add(frame);
        Canvas.SetLeft(tint, LeftPadding);
        Canvas.SetTop(tint, TopPadding);
        _canvas.Children.Add(tint);
    }

    private void DrawGrid(double width, double height, DateTimeOffset start, DateTimeOffset end, double maxValue, bool includeLabels = true)
    {
        var gridBrush = ResolveBrush("SurfaceGridBrush", "#35587A");
        var plotWidth = Math.Max(16d, width - LeftPadding - RightPadding);
        var plotHeight = Math.Max(16d, height - TopPadding - BottomPadding);
        var bottomY = TopPadding + plotHeight;
        var divisions = width >= 760 ? 5 : width >= 540 ? 4 : 3;

        for (var index = 0; index < divisions; index++)
        {
            var y = TopPadding + ((plotHeight / divisions) * index);
            _canvas.Children.Add(new Rectangle
            {
                Width = plotWidth,
                Height = plotHeight / divisions,
                Fill = ResolveBrush("SurfaceGridBrush", index % 2 == 0 ? "#274768" : "#1B3650"),
                Opacity = index % 2 == 0 ? 0.15 : 0.07,
            });

            Canvas.SetLeft(_canvas.Children[^1], LeftPadding);
            Canvas.SetTop(_canvas.Children[^1], y);
        }

        for (var index = 0; index <= divisions; index++)
        {
            var y = TopPadding + ((plotHeight / divisions) * index);
            _canvas.Children.Add(new Line
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                Opacity = index is 0 || index == divisions ? 0.58 : 0.34,
                X1 = LeftPadding,
                X2 = LeftPadding + plotWidth,
                Y1 = y,
                Y2 = y,
            });
        }

        for (var index = 0; index <= divisions; index++)
        {
            var x = LeftPadding + ((plotWidth / divisions) * index);
            if (index is not 0 && index != divisions)
            {
                _canvas.Children.Add(new Line
                {
                    Stroke = gridBrush,
                    StrokeThickness = 1,
                    Opacity = 0.18,
                    X1 = x,
                    X2 = x,
                    Y1 = TopPadding,
                    Y2 = bottomY,
                });
            }

            if (!includeLabels)
            {
                continue;
            }

            var tick = start + TimeSpan.FromTicks((end - start).Ticks / divisions * index);
            var label = new TextBlock
            {
                FontSize = 9,
                Foreground = ResolveBrush("TextSecondaryBrush", "#A8C2DA"),
                Text = FormatTimeLabel(tick, end - start),
            };
            _canvas.Children.Add(label);
            Canvas.SetLeft(label, Math.Clamp(x - 18, LeftPadding, width - 54));
            Canvas.SetTop(label, bottomY + 4);
        }

        if (!includeLabels)
        {
            return;
        }

        var ceiling = new TextBlock
        {
            FontSize = 9,
            Foreground = ResolveBrush("TextSecondaryBrush", "#A8C2DA"),
            Text = FormatAxisValue(maxValue, Unit),
        };
        _canvas.Children.Add(ceiling);
        Canvas.SetLeft(ceiling, LeftPadding);
        Canvas.SetTop(ceiling, 2);

        var floor = new TextBlock
        {
            FontSize = 9,
            Foreground = ResolveBrush("TextSecondaryBrush", "#A8C2DA"),
            Text = FormatAxisValue(0d, Unit),
        };
        _canvas.Children.Add(floor);
        Canvas.SetLeft(floor, LeftPadding);
        Canvas.SetTop(floor, bottomY - 14);
    }

    private double ResolveMaxValue(double peak)
    {
        if (CeilingValue > 0d)
        {
            return CeilingValue;
        }

        if (Unit == MetricUnit.Percent)
        {
            return 100d;
        }

        if (peak <= 0d)
        {
            return Unit == MetricUnit.Gigabytes ? 1d : 100d;
        }

        return peak * 1.14d;
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

    private static int ResolvePointBudget(double width, int seriesCount, TimeSpan window)
    {
        var density = seriesCount switch
        {
            <= 1 => 0.72d,
            2 => 0.58d,
            <= 4 => 0.42d,
            <= 8 => 0.3d,
            _ => 0.22d,
        };

        var windowFactor = window switch
        {
            _ when window >= TimeSpan.FromDays(30) => 0.24d,
            _ when window >= TimeSpan.FromDays(7) => 0.3d,
            _ when window >= TimeSpan.FromDays(1) => 0.38d,
            _ when window >= TimeSpan.FromHours(12) => 0.5d,
            _ when window >= TimeSpan.FromHours(1) => 0.66d,
            _ => 0.88d,
        };

        return Math.Max(24, (int)Math.Round(width * density * windowFactor));
    }

    private static IReadOnlyList<MetricPoint> GetRenderablePoints(
        IReadOnlyList<MetricPoint> points,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        if (points.Count == 0)
        {
            return Array.Empty<MetricPoint>();
        }

        var filtered = new List<MetricPoint>(points.Count);

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point.Timestamp >= start && point.Timestamp <= end)
            {
                filtered.Add(point);
            }
        }

        if (filtered.Count > 0)
        {
            return filtered;
        }

        var nearest = FindNearestPoint(points, start);
        return nearest is null
            ? Array.Empty<MetricPoint>()
            :
            [
                new MetricPoint(start, nearest.Value),
                new MetricPoint(end, nearest.Value),
            ];
    }

    private static IReadOnlyList<Windows.Foundation.Point> NormalizeProjectedPoints(IEnumerable<Windows.Foundation.Point> points)
    {
        var source = points as Windows.Foundation.Point[] ?? points.ToArray();
        if (source.Length == 0)
        {
            return source;
        }

        var normalized = new List<Windows.Foundation.Point>(source.Length);
        var active = source[0];
        normalized.Add(active);

        for (var index = 1; index < source.Length; index++)
        {
            var point = source[index];
            if (Math.Abs(point.X - active.X) < 0.8d)
            {
                active = point;
                normalized[^1] = point;
                continue;
            }

            active = point;
            normalized.Add(point);
        }

        return normalized;
    }

    private static IReadOnlyList<Windows.Foundation.Point> ResolveStrokePoints(IReadOnlyList<Windows.Foundation.Point> points)
    {
        if (points.Count <= 1)
        {
            return points;
        }

        var startIndex = ResolveLeadingArtifactTrimIndex(points);

        return startIndex == 0 ? points : points.Skip(startIndex).ToArray();
    }

    private static IReadOnlyList<Windows.Foundation.Point> ResolveFillPoints(IReadOnlyList<Windows.Foundation.Point> points)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        var startIndex = ResolveLeadingArtifactTrimIndex(points);

        if (startIndex >= points.Count - 1)
        {
            return points;
        }

        var candidatePoints = startIndex == 0 ? points : points.Skip(startIndex).ToArray();
        if (candidatePoints.Count > 1 &&
            candidatePoints[0].X <= LeftPadding + 20d &&
            candidatePoints[1].X - candidatePoints[0].X >= 28d)
        {
            return candidatePoints.Skip(1).ToArray();
        }

        return candidatePoints;
    }

    private static int ResolveLeadingArtifactTrimIndex(IReadOnlyList<Windows.Foundation.Point> points)
    {
        if (points.Count <= 1)
        {
            return 0;
        }

        var startIndex = 0;
        while (startIndex < points.Count - 1)
        {
            var current = points[startIndex];
            var next = points[startIndex + 1];
            var nearLeadingEdge = current.X <= LeftPadding + 10d;
            var compressedX = Math.Abs(next.X - current.X) < 0.8d;

            if (!nearLeadingEdge || !compressedX)
            {
                break;
            }

            startIndex++;
        }

        return startIndex;
    }

    private static bool TryGetPointBounds(
        IReadOnlyList<ChartSeriesViewModel> series,
        out DateTimeOffset minTimestamp,
        out DateTimeOffset maxTimestamp,
        out double maxObservedValue)
    {
        minTimestamp = default;
        maxTimestamp = default;
        maxObservedValue = 0d;
        var found = false;

        foreach (var item in series)
        {
            foreach (var point in item.Points)
            {
                if (!found)
                {
                    minTimestamp = point.Timestamp;
                    maxTimestamp = point.Timestamp;
                    maxObservedValue = point.Value;
                    found = true;
                    continue;
                }

                if (point.Timestamp < minTimestamp)
                {
                    minTimestamp = point.Timestamp;
                }

                if (point.Timestamp > maxTimestamp)
                {
                    maxTimestamp = point.Timestamp;
                }

                if (point.Value > maxObservedValue)
                {
                    maxObservedValue = point.Value;
                }
            }
        }

        return found;
    }

    private static Windows.Foundation.Point Project(
        MetricPoint point,
        double leftPadding,
        double topPadding,
        double plotWidth,
        double plotHeight,
        DateTimeOffset start,
        DateTimeOffset end,
        double maxValue)
    {
        var totalMs = Math.Max(1d, (end - start).TotalMilliseconds);
        var xRatio = (point.Timestamp - start).TotalMilliseconds / totalMs;
        var yRatio = maxValue <= 0d ? 0d : point.Value / maxValue;

        return new Windows.Foundation.Point(
            leftPadding + (Math.Clamp(xRatio, 0d, 1d) * plotWidth),
            topPadding + plotHeight - (Math.Clamp(yRatio, 0d, 1d) * plotHeight));
    }

    private static IReadOnlyList<IReadOnlyList<MetricPoint>> SplitAtGaps(
        IReadOnlyList<MetricPoint> points,
        TimeSpan windowSpan)
    {
        if (points.Count <= 1)
        {
            return [points];
        }

        // Detect the typical interval between points, then treat anything >3x that as a gap
        var medianIntervalMs = 0d;
        if (points.Count >= 3)
        {
            var intervals = new List<double>(points.Count - 1);
            for (var i = 1; i < points.Count; i++)
            {
                intervals.Add((points[i].Timestamp - points[i - 1].Timestamp).TotalMilliseconds);
            }
            intervals.Sort();
            medianIntervalMs = intervals[intervals.Count / 2];
        }

        // Minimum gap threshold: 3x the median interval, but at least 10 seconds
        var gapThresholdMs = Math.Max(medianIntervalMs * 3d, 10_000d);

        var segments = new List<IReadOnlyList<MetricPoint>>();
        var current = new List<MetricPoint> { points[0] };

        for (var i = 1; i < points.Count; i++)
        {
            var delta = (points[i].Timestamp - points[i - 1].Timestamp).TotalMilliseconds;
            if (delta > gapThresholdMs)
            {
                if (current.Count > 0)
                {
                    segments.Add(current.ToArray());
                }
                current = [];
            }
            current.Add(points[i]);
        }

        if (current.Count > 0)
        {
            segments.Add(current.ToArray());
        }

        return segments;
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

    private static string FormatTimeLabel(DateTimeOffset timestamp, TimeSpan window)
    {
        if (window <= TimeSpan.FromMinutes(5))
        {
            return timestamp.LocalDateTime.ToString("HH:mm:ss");
        }

        if (window <= TimeSpan.FromHours(12))
        {
            return timestamp.LocalDateTime.ToString("HH:mm");
        }

        if (window <= TimeSpan.FromDays(2))
        {
            return timestamp.LocalDateTime.ToString("MM-dd HH:mm");
        }

        return timestamp.LocalDateTime.ToString("MM-dd");
    }

    private static string FormatAxisValue(double value, MetricUnit unit) => unit switch
    {
        MetricUnit.Percent => $"{value:0.#}%",
        MetricUnit.Celsius => $"{value:0.#} C",
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

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        HideHover();
        _isSelecting = true;
        _selectionStartX = point.Position.X;
        _selectionCurrentX = _selectionStartX;
        UpdateSelectionVisual();
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            UpdateHover(e.GetCurrentPoint(this).Position);
            return;
        }

        _selectionCurrentX = e.GetCurrentPoint(this).Position.X;
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        ReleasePointerCaptures();
        CompleteSelection();
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        CompleteSelection();
    }

    private void CompleteSelection()
    {
        _isSelecting = false;
        _selectionRectangle.Visibility = Visibility.Collapsed;

        if (WindowEndUtc <= WindowStartUtc)
        {
            return;
        }

        var left = Math.Min(_selectionStartX, _selectionCurrentX);
        var right = Math.Max(_selectionStartX, _selectionCurrentX);
        if (right - left < MinimumSelectionWidth)
        {
            return;
        }

        var plotWidth = Math.Max(16d, ActualWidth - LeftPadding - RightPadding);
        var clampedLeft = Math.Clamp(left, LeftPadding, LeftPadding + plotWidth);
        var clampedRight = Math.Clamp(right, LeftPadding, LeftPadding + plotWidth);
        if (clampedRight - clampedLeft < MinimumSelectionWidth)
        {
            return;
        }

        var normalizedLeft = (clampedLeft - LeftPadding) / plotWidth;
        var normalizedRight = (clampedRight - LeftPadding) / plotWidth;
        var start = WindowStartUtc + TimeSpan.FromTicks((long)((WindowEndUtc - WindowStartUtc).Ticks * normalizedLeft));
        var end = WindowStartUtc + TimeSpan.FromTicks((long)((WindowEndUtc - WindowStartUtc).Ticks * normalizedRight));

        if (end > start)
        {
            ZoomSelectionRequested?.Invoke(this, new ChartZoomSelectionEventArgs(start, end));
        }
    }

    private void UpdateSelectionVisual()
    {
        var left = Math.Min(_selectionStartX, _selectionCurrentX);
        var width = Math.Abs(_selectionCurrentX - _selectionStartX);
        _selectionRectangle.Visibility = width >= 2 ? Visibility.Visible : Visibility.Collapsed;
        _selectionRectangle.Width = width;
        _selectionRectangle.Height = Math.Max(0d, ActualHeight - BottomPadding);
        Canvas.SetLeft(_selectionRectangle, left);
        Canvas.SetTop(_selectionRectangle, 0);
    }

    private void UpdateHover(Windows.Foundation.Point position)
    {
        if (_isSelecting || Series is null || Series.Count == 0)
        {
            HideHover();
            return;
        }

        var plotWidth = Math.Max(16d, ActualWidth - LeftPadding - RightPadding);
        var plotHeight = Math.Max(16d, ActualHeight - TopPadding - BottomPadding);
        if (position.X < LeftPadding || position.X > LeftPadding + plotWidth || position.Y < TopPadding || position.Y > TopPadding + plotHeight)
        {
            HideHover();
            return;
        }

        if (!TryBuildHoverSnapshot(position.X, plotWidth, plotHeight, out var hoverX, out var tooltip))
        {
            HideHover();
            return;
        }

        _hoverLine.Visibility = Visibility.Visible;
        _hoverLine.X1 = hoverX;
        _hoverLine.X2 = hoverX;
        _hoverLine.Y1 = TopPadding;
        _hoverLine.Y2 = TopPadding + plotHeight;

        _hoverTooltipText.Text = tooltip;
        _hoverTooltip.Measure(new Windows.Foundation.Size(240, double.PositiveInfinity));
        var desired = _hoverTooltip.DesiredSize;
        var tooltipLeft = hoverX > (ActualWidth - desired.Width - 20)
            ? hoverX - desired.Width - 12
            : hoverX + 12;
        var tooltipTop = Math.Max(8, TopPadding + 10);
        _hoverTooltip.Visibility = Visibility.Visible;
        Canvas.SetLeft(_hoverTooltip, Math.Clamp(tooltipLeft, 8, Math.Max(8, ActualWidth - desired.Width - 8)));
        Canvas.SetTop(_hoverTooltip, tooltipTop);
    }

    private bool TryBuildHoverSnapshot(double pointerX, double plotWidth, double plotHeight, out double hoverX, out string tooltip)
    {
        hoverX = 0d;
        tooltip = string.Empty;

        if (!TryGetPointBounds(Series, out _, out _, out var peak))
        {
            return false;
        }

        var start = WindowStartUtc;
        var end = WindowEndUtc;
        if (end <= start)
        {
            return false;
        }

        var maxValue = ResolveMaxValue(peak);
        var normalized = Math.Clamp((pointerX - LeftPadding) / plotWidth, 0d, 1d);
        var target = start + TimeSpan.FromTicks((long)((end - start).Ticks * normalized));
        var lines = new List<(string Name, double Value)>();
        DateTimeOffset? anchorTime = null;
        var bestDistance = TimeSpan.MaxValue;

        foreach (var series in Series.Where(item => item.Points.Count > 0))
        {
            var nearestPoint = FindNearestPoint(series.Points, target);
            if (nearestPoint is null)
            {
                continue;
            }

            var distance = (nearestPoint.Timestamp - target).Duration();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                anchorTime = nearestPoint.Timestamp;
            }

            lines.Add((series.Name, nearestPoint.Value));
        }

        if (anchorTime is null || lines.Count == 0)
        {
            return false;
        }

        hoverX = Project(
            new MetricPoint(anchorTime.Value, 0d),
            LeftPadding,
            TopPadding,
            plotWidth,
            plotHeight,
            start,
            end,
            Math.Max(maxValue, 1d)).X;

        var builder = new StringBuilder();
        builder.AppendLine(FormatHoverTime(anchorTime.Value, end - start));
        foreach (var line in lines.OrderByDescending(item => item.Value).Take(5))
        {
            builder.AppendLine($"- {line.Name}  {FormatAxisValue(line.Value, Unit)}");
        }

        if (lines.Count > 5)
        {
            builder.Append($"+{lines.Count - 5} more");
        }

        tooltip = builder.ToString().TrimEnd();
        return true;
    }

    private static MetricPoint? FindNearestPoint(IReadOnlyList<MetricPoint> points, DateTimeOffset target)
    {
        if (points.Count == 0)
        {
            return null;
        }

        MetricPoint best = points[0];
        var bestDistance = (best.Timestamp - target).Duration();
        for (var index = 1; index < points.Count; index++)
        {
            var current = points[index];
            var distance = (current.Timestamp - target).Duration();
            if (distance >= bestDistance)
            {
                continue;
            }

            best = current;
            bestDistance = distance;
        }

        return best;
    }

    private void HideHover()
    {
        _hoverLine.Visibility = Visibility.Collapsed;
        _hoverTooltip.Visibility = Visibility.Collapsed;
    }

    private static string FormatHoverTime(DateTimeOffset timestamp, TimeSpan window)
    {
        return window <= TimeSpan.FromMinutes(5)
            ? timestamp.LocalDateTime.ToString("h:mm:ss tt")
            : timestamp.LocalDateTime.ToString("h:mm:ss tt");
    }
}

public sealed class ChartZoomSelectionEventArgs : EventArgs
{
    public ChartZoomSelectionEventArgs(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }
}
