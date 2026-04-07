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
        UseLayoutRounding = true;

        _surface = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(13, 7, 13, 7),
            Background = ResolveBrush("SurfaceElevatedBrush", "#111D2A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#243D55"),
        };

        _shine = new Border
        {
            Height = 0,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1, 1, 1, 0),
            Opacity = 0,
            Visibility = Visibility.Collapsed,
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
            FontSize = 11,
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

        IsTabStop = true;
        KeyDown += OnKeyDown;
        GotFocus += (_, _) => { _isHovered = true; UpdateVisualState(); };
        LostFocus += (_, _) => { _isHovered = false; UpdateVisualState(); };

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

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
        {
            Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public void RefreshThemeResources()
    {
        UpdateVisualState();
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
        var isLight = IsLightPaletteActive();

        if (isLight)
        {
            _surface.Background = useAccent
                ? ResolveBrush("AccentSoftBrush", "#C8E8F8")
                : ResolveBrush(_isHovered ? "SurfaceStrongBrush" : "SurfaceElevatedBrush", _isHovered ? "#E0EAF4" : "#F0F5FB");
            _surface.BorderBrush = useAccent
                ? ResolveBrush("AccentStrongBrush", "#065A82")
                : ResolveBrush("SurfaceStrokeBrush", _isHovered ? "#B0C4D8" : "#C4D2E2");
        }
        else
        {
            _surface.Background = useAccent
                ? CreateSurfaceGradient("#18344B", "#122437")
                : CreateSurfaceGradient(_isHovered ? "#132232" : "#101A28", _isHovered ? "#17293D" : "#122031");
            _surface.BorderBrush = useAccent
                ? ResolveBrush("AccentStrongBrush", "#9EEFFF")
                : ResolveBrush("SurfaceStrokeBrush", _isHovered ? "#2C4866" : "#243D55");
        }

        Opacity = _isPressed ? 0.82 : _isHovered ? 0.95 : 1.0;
        _label.Foreground = ResolveBrush("TextPrimaryBrush", isLight ? "#0A1824" : "#F2F8FF");
        _glow.Opacity = 0;
        _shine.Opacity = 0;
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
