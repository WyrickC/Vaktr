using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

public sealed class ActionChip : UserControl
{
    private readonly Border _surface;
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
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
        };

        _label = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        _surface.Child = _label;
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
        var backgroundKey = useAccent ? "AccentSoftBrush" : "SurfaceElevatedBrush";
        var backgroundHex = useAccent ? "#10394D" : "#15283B";
        var hoverKey = useAccent ? "AccentHaloBrush" : "SurfaceStrongBrush";
        var hoverHex = useAccent ? "#1B68DAFF" : "#183148";

        _surface.Background = _isHovered
            ? ResolveBrush(hoverKey, hoverHex)
            : ResolveBrush(backgroundKey, backgroundHex);

        _surface.BorderBrush = _isActive
            ? ResolveBrush("AccentStrongBrush", "#B7F7FF")
            : ResolveBrush("SurfaceStrokeBrush", "#27425E");

        Opacity = _isPressed ? 0.84 : 1.0;
        _label.Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF");
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
