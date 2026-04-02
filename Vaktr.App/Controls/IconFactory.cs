using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

internal static class IconFactory
{
    private static readonly FontFamily FluentIconFont = new("Segoe Fluent Icons");

    public static FrameworkElement CreateTile(string key, Brush accentBrush, double size = 44, double iconSize = 18)
    {
        var corner = Math.Max(12, size * 0.28);

        return new Border
        {
            Width = size,
            Height = size,
            Background = CreateSurfaceGradient("#102031", "#15273D"),
            BorderBrush = CreateOpacityBrush(accentBrush, 0.34),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(corner),
            Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Margin = new Thickness(2),
                        CornerRadius = new CornerRadius(Math.Max(10, corner - 2)),
                        Background = CreateInnerGlowBrush(accentBrush),
                        Opacity = 0.16,
                        IsHitTestVisible = false,
                    },
                    new Border
                    {
                        Height = 1,
                        Margin = new Thickness(1, 1, 1, 0),
                        CornerRadius = new CornerRadius(1),
                        Background = CreateOpacityBrush(accentBrush, 0.18),
                        VerticalAlignment = VerticalAlignment.Top,
                        IsHitTestVisible = false,
                    },
                    new Border
                    {
                        Margin = new Thickness(5),
                        CornerRadius = new CornerRadius(Math.Max(8, corner - 5)),
                        BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                        BorderThickness = new Thickness(1),
                        Opacity = 0.35,
                        IsHitTestVisible = false,
                    },
                    CreateIcon(key, accentBrush, iconSize),
                },
            },
        };
    }

    public static FrameworkElement CreateIcon(string key, Brush accentBrush, double size = 18)
    {
        var glyph = ResolveGlyph(Normalize(key));
        var glowBrush = CreateOpacityBrush(accentBrush, 0.18);

        return new Grid
        {
            Width = size + 8,
            Height = size + 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children =
            {
                new Ellipse
                {
                    Width = size + 2,
                    Height = size + 2,
                    Fill = glowBrush,
                    Opacity = 0.28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                },
                new FontIcon
                {
                    Glyph = glyph,
                    FontFamily = FluentIconFont,
                    FontSize = size,
                    Foreground = accentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                },
            },
        };
    }

    private static string ResolveGlyph(string key)
    {
        return key switch
        {
            "collection" => "\uF182", // ScreenTime
            "retention" => "\uE81C", // History
            "storage" => "\uE8B7", // Folder
            "cpu" => "\uEEA1", // CPU
            "memory" => "\uEEA0", // RAM
            "disk" => "\uEDA2", // HardDrive
            "drive" => "\uE8CE", // MapDrive
            "network" => "\uEDA3", // NetworkAdapter
            _ => "\uF182",
        };
    }

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
        if (IsLightPaletteActive())
        {
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = ResolveColor("SurfaceElevatedBrush", "#F4F8FC"), Offset = 0d },
                    new GradientStop { Color = ResolveColor("SurfaceStrongBrush", "#EDF4FB"), Offset = 1d },
                },
            };
        }

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

    private static Brush CreateInnerGlowBrush(Brush accentBrush)
    {
        if (accentBrush is SolidColorBrush solidBrush)
        {
            var glowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.36),
                GradientOrigin = new Point(0.5, 0.36),
                RadiusX = 0.92,
                RadiusY = 0.92,
            };

            glowBrush.GradientStops.Add(new GradientStop { Color = solidBrush.Color, Offset = 0d });
            glowBrush.GradientStops.Add(new GradientStop { Color = solidBrush.Color, Offset = 0.32d });
            glowBrush.GradientStops.Add(new GradientStop { Color = BrushFactory.ParseColor("#001018"), Offset = 1d });
            return glowBrush;
        }

        return CreateSurfaceGradient("#143249", "#0C1622");
    }

    private static bool IsLightPaletteActive()
    {
        var color = ResolveColor("AppBackdropBrush", "#030812");
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance >= 170d;
    }

    private static Windows.UI.Color ResolveColor(string key, string fallbackHex)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return BrushFactory.ParseColor(fallbackHex);
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
    }

    private static Brush CreateOpacityBrush(Brush brush, double opacity)
    {
        if (brush is SolidColorBrush solidBrush)
        {
            return new SolidColorBrush(solidBrush.Color) { Opacity = opacity };
        }

        return new SolidColorBrush(BrushFactory.ParseColor("#66E7FF")) { Opacity = opacity };
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
            "collection" or "clock" or "scrape" => "collection",
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
