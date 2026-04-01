using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

public sealed class ActionChip : UserControl
{
    private readonly Border _surface;
    private readonly Border _shine;
    private readonly Border _glow;
    private readonly TextBlock _label;
    private bool _isActive;
    private bool _isFilled;
    private bool _isHovered;
    private bool _isPressed;
    private string _text = string.Empty;

    public ActionChip()
    {
        _surface = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(15, 9, 15, 9),
            Background = ResolveBrush("SurfaceElevatedBrush", "#101D2A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#233D56"),
        };

        _shine = new Border
        {
            Height = 1,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1, 1, 1, 0),
            Opacity = 0.12,
            IsHitTestVisible = false,
        };

        _glow = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(9),
            Opacity = 0,
            IsHitTestVisible = false,
        };

        _label = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        _surface.Child = new Grid
        {
            Children =
            {
                _glow,
                _shine,
                _label,
            },
        };
        Content = _surface;

        _surface.PointerEntered += OnPointerEntered;
        _surface.PointerExited += OnPointerExited;
        _surface.PointerPressed += OnPointerPressed;
        _surface.PointerReleased += OnPointerReleased;
        _surface.Tapped += OnTapped;

        UpdateVisualState();
    }

    public event EventHandler? Click;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _label.Text = value;
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            UpdateVisualState();
        }
    }

    public bool IsFilled
    {
        get => _isFilled;
        set
        {
            if (_isFilled == value)
            {
                return;
            }

            _isFilled = value;
            UpdateVisualState();
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        Click?.Invoke(this, EventArgs.Empty);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = true;
        UpdateVisualState();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        UpdateVisualState();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = true;
        UpdateVisualState();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = false;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        var useAccent = _isActive || _isFilled;
        _surface.Background = useAccent
            ? CreateSurfaceGradient("#1C3C5D", "#11253B")
            : CreateSurfaceGradient(_isHovered ? "#17263A" : "#101C2C", _isHovered ? "#1A2C43" : "#132133");

        _surface.BorderBrush = useAccent
            ? ResolveBrush("AccentStrongBrush", "#94F0FF")
            : ResolveBrush("SurfaceStrokeBrush", _isHovered ? "#355170" : "#233D56");

        Opacity = _isPressed ? 0.9 : 1.0;
        _label.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _glow.Background = ResolveBrush("AccentHaloBrush", "#1B68DAFF");
        _glow.Opacity = useAccent ? 0.12 : _isHovered ? 0.05 : 0;
        _shine.Background = useAccent
            ? ResolveBrush("AccentStrongBrush", "#D7FBFF")
            : ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _shine.Opacity = useAccent ? 0.3 : _isHovered ? 0.16 : 0.09;
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
                    new GradientStop { Color = ResolveColor("SurfaceElevatedBrush", "#F4F8FC"), Offset = 0d },
                    new GradientStop { Color = ResolveColor("SurfaceStrongBrush", "#EDF4FB"), Offset = 1d },
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
}
