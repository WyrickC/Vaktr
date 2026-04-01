using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

internal static class IconFactory
{
    public static FrameworkElement CreateTile(string key, Brush accentBrush, double size = 44, double iconSize = 18)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Ellipse
                {
                    Width = size * 0.92,
                    Height = size * 0.92,
                    Fill = accentBrush,
                    Opacity = 0.11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                },
                new Border
                {
                    Background = CreateSurfaceGradient("#12243A", "#172E49"),
                    BorderBrush = accentBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(Math.Max(12, size * 0.28)),
                    Child = new Grid
                    {
                        Children =
                        {
                            CreateIcon(key, accentBrush, iconSize),
                        },
                    },
                },
            },
        };
    }

    public static FrameworkElement CreateIcon(string key, Brush accentBrush, double size = 18)
    {
        return Normalize(key) switch
        {
            "collection" => CreateClockIcon(accentBrush, size),
            "retention" => CreateBoltIcon(accentBrush, size),
            "storage" => CreateFolderIcon(accentBrush, size),
            "cpu" => CreateCpuIcon(accentBrush, size),
            "memory" => CreateMemoryIcon(accentBrush, size),
            "disk" => CreateDiskIcon(accentBrush, size),
            "drive" => CreateDriveIcon(accentBrush, size),
            "network" => CreateNetworkIcon(accentBrush, size),
            _ => CreateClockIcon(accentBrush, size),
        };
    }

    private static FrameworkElement CreateClockIcon(Brush stroke, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Ellipse
                {
                    Width = size * 0.86,
                    Height = size * 0.86,
                    Stroke = stroke,
                    StrokeThickness = 1.7,
                },
                new Line
                {
                    X1 = size * 0.5,
                    Y1 = size * 0.5,
                    X2 = size * 0.5,
                    Y2 = size * 0.26,
                    Stroke = stroke,
                    StrokeThickness = 1.7,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                },
                new Line
                {
                    X1 = size * 0.5,
                    Y1 = size * 0.5,
                    X2 = size * 0.72,
                    Y2 = size * 0.58,
                    Stroke = stroke,
                    StrokeThickness = 1.7,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                },
            },
        };
    }

    private static FrameworkElement CreateBoltIcon(Brush fill, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Polygon
                {
                    Fill = fill,
                    Stretch = Stretch.Fill,
                    Points = new PointCollection
                    {
                        new(size * 0.58, size * 0.04),
                        new(size * 0.22, size * 0.58),
                        new(size * 0.46, size * 0.58),
                        new(size * 0.38, size * 0.96),
                        new(size * 0.78, size * 0.38),
                        new(size * 0.54, size * 0.38),
                    },
                },
            },
        };
    }

    private static FrameworkElement CreateFolderIcon(Brush stroke, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Border
                {
                    Width = size * 0.36,
                    Height = size * 0.18,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(size * 0.08, size * 0.12, 0, 0),
                    BorderBrush = stroke,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                },
                new Border
                {
                    Width = size * 0.82,
                    Height = size * 0.54,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, size * 0.12),
                    BorderBrush = stroke,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(4),
                },
            },
        };
    }

    private static FrameworkElement CreateCpuIcon(Brush stroke, double size)
    {
        var inner = new Border
        {
            Width = size * 0.32,
            Height = size * 0.32,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1.4),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var outer = new Border
        {
            Width = size * 0.62,
            Height = size * 0.62,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1.6),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var host = new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                outer,
                inner,
            },
        };

        AddPin(host, stroke, size * 0.16, size * 0.08, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, size * 0.02, 0, 0));
        AddPin(host, stroke, size * 0.16, size * 0.08, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, size * 0.02));
        AddPin(host, stroke, size * 0.08, size * 0.16, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(size * 0.02, 0, 0, 0));
        AddPin(host, stroke, size * 0.08, size * 0.16, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, size * 0.02, 0));
        return host;
    }

    private static FrameworkElement CreateMemoryIcon(Brush stroke, double size)
    {
        var host = new Grid
        {
            Width = size,
            Height = size,
        };

        host.Children.Add(new Border
        {
            Width = size * 0.84,
            Height = size * 0.48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
        });

        for (var index = 0; index < 4; index++)
        {
            host.Children.Add(new Border
            {
                Width = size * 0.08,
                Height = size * 0.18,
                Background = stroke,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(size * (0.2 + (index * 0.14)), 0, 0, 0),
            });
        }

        return host;
    }

    private static FrameworkElement CreateDiskIcon(Brush stroke, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Border
                {
                    Width = size * 0.84,
                    Height = size * 0.54,
                    BorderBrush = stroke,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                CreateDiskSlot(stroke, size, 0.42),
                CreateDiskSlot(stroke, size, 0.58),
            },
        };
    }

    private static FrameworkElement CreateDriveIcon(Brush stroke, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                new Ellipse
                {
                    Width = size * 0.84,
                    Height = size * 0.84,
                    Stroke = stroke,
                    StrokeThickness = 1.6,
                },
                new Line
                {
                    X1 = size * 0.5,
                    Y1 = size * 0.5,
                    X2 = size * 0.76,
                    Y2 = size * 0.36,
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                },
            },
        };
    }

    private static FrameworkElement CreateNetworkIcon(Brush stroke, double size)
    {
        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                CreateNetworkLine(stroke, size * 0.24, size * 0.7, size * 0.5, size * 0.3),
                CreateNetworkLine(stroke, size * 0.5, size * 0.3, size * 0.78, size * 0.66),
                CreateNode(stroke, size * 0.18, size * 0.64, size * 0.18),
                CreateNode(stroke, size * 0.44, size * 0.24, size * 0.18),
                CreateNode(stroke, size * 0.72, size * 0.6, size * 0.18),
            },
        };
    }

    private static Border CreateDiskSlot(Brush stroke, double size, double yFactor)
    {
        return new Border
        {
            Width = size * 0.46,
            Height = 1.5,
            Background = stroke,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, size * yFactor, 0, 0),
        };
    }

    private static Ellipse CreateNode(Brush stroke, double x, double y, double size)
    {
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = stroke,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(x, y, 0, 0),
        };
    }

    private static Line CreateNetworkLine(Brush stroke, double x1, double y1, double x2, double y2)
    {
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = 1.4,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round,
        };
    }

    private static void AddPin(Grid host, Brush fill, double width, double height, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, Thickness margin)
    {
        host.Children.Add(new Border
        {
            Width = width,
            Height = height,
            Background = fill,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            Margin = margin,
        });
    }

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = BrushFactory.ParseColor(startHex), Offset = 0d },
                new GradientStop { Color = BrushFactory.ParseColor(endHex), Offset = 1d },
            },
        };
    }

    private static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Contains("storage"))
        {
            return "storage";
        }

        return normalized switch
        {
            "collection" or "clock" => "collection",
            "retention" or "history" or "bolt" => "retention",
            "storage" or "folder" => "storage",
            "cpu" => "cpu",
            "mem" or "memory" or "ram" => "memory",
            "disk" or "dsk" => "disk",
            "drive" or "drv" or "volume" => "drive",
            "network" or "net" or "wan" => "network",
            _ => key.Trim().ToLowerInvariant(),
        };
    }
}
