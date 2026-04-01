using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
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
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(13, 8, 13, 8),
            Background = ResolveBrush("SurfaceElevatedBrush", "#101D2A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#233D56"),
        };

        _label = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11.5,
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
        _surface.Background = useAccent
            ? ResolveBrush("AccentSoftBrush", "#10394D")
            : ResolveBrush(_isHovered ? "SurfaceStrongBrush" : "SurfaceElevatedBrush", _isHovered ? "#183148" : "#15283B");

        _surface.BorderBrush = useAccent
            ? ResolveBrush("AccentStrongBrush", "#94F0FF")
            : ResolveBrush("SurfaceStrokeBrush", _isHovered ? "#31516F" : "#233D56");

        Opacity = _isPressed ? 0.9 : 1.0;
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
