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
        var isLight = IsLightPaletteActive();

        if (isLight && accentBrush is SolidColorBrush lightSolid)
        {
            var c = lightSolid.Color;
            var darkC = DarkenColor(c, 0.5);
            return new Border
            {
                Width = size,
                Height = size,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, darkC.R, darkC.G, darkC.B)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60, darkC.R, darkC.G, darkC.B)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(corner),
                Child = CreateIcon(key, accentBrush, iconSize),
            };
        }

        return new Border
        {
            Width = size,
            Height = size,
            Background = CreateSurfaceGradient("#102031", "#15273D"),
            BorderBrush = CreateOpacityBrush(accentBrush, 0.4),
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
                    CreateIcon(key, accentBrush, iconSize),
                },
            },
        };
    }

    public static FrameworkElement CreateIcon(string key, Brush accentBrush, double size = 18)
    {
        var glyph = ResolveGlyph(Normalize(key));
        var isLight = IsLightPaletteActive();

        // In light mode, darken the icon for strong contrast on light backgrounds
        var iconBrush = isLight && accentBrush is SolidColorBrush solid
            ? new SolidColorBrush(DarkenColor(solid.Color, 0.5))
            : accentBrush;

        return new FontIcon
        {
            Glyph = glyph,
            FontFamily = FluentIconFont,
            FontSize = size,
            Foreground = iconBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
    }

    private static Windows.UI.Color DarkenColor(Windows.UI.Color color, double factor)
    {
        return Windows.UI.Color.FromArgb(
            color.A,
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor));
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
            "gpu" => "\uE7F8", // Video
            "temperature" => "\uE9CA", // Thermometer
            "system" => "\uF182", // ScreenTime
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
            "temperature" or "temp" => "temperature",
            "gpu" or "graphics" => "gpu",
            "system" or "activity" or "host" => "system",
            _ => key.Trim().ToLowerInvariant(),
        };
    }
}
