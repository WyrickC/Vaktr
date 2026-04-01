using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

public sealed class InlineTextEntry : UserControl
{
    private readonly Border _surface;
    private readonly Border _shine;
    private readonly Border _glow;
    private readonly TextBlock _label;
    private bool _isFocused;
    private bool _isHovered;
    private bool _isPressed;
    private string _text = string.Empty;
    private string _placeholderText = string.Empty;

    public InlineTextEntry()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = false;

        _label = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 14,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _surface = new Border
        {
            Background = CreateSurfaceGradient("#101C2E", "#14253A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(15, 12, 15, 12),
            MinHeight = 48,
        };

        _shine = new Border
        {
            Height = 1,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1, 1, 1, 0),
            Opacity = 0.1,
            IsHitTestVisible = false,
        };

        _glow = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(10),
            Opacity = 0,
            IsHitTestVisible = false,
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

        Tapped += OnTapped;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        KeyDown += OnKeyDown;
        CharacterReceived += OnCharacterReceived;

        UpdateVisualState();
    }

    public event EventHandler? TextChanged;

    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            _text = value;
            UpdateVisualState();
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_placeholderText, value, StringComparison.Ordinal))
            {
                return;
            }

            _placeholderText = value;
            UpdateVisualState();
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
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

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = true;
        UpdateVisualState();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = false;
        _isPressed = false;
        UpdateVisualState();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Back:
                if (_text.Length > 0)
                {
                    Text = _text[..^1];
                }

                e.Handled = true;
                break;
            case VirtualKey.Escape:
                _ = Focus(FocusState.Programmatic);
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                e.Handled = true;
                break;
        }
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        var character = args.Character;
        if (char.IsControl(character))
        {
            return;
        }

        Text += character;
        args.Handled = true;
    }

    private void UpdateVisualState()
    {
        _surface.Background = CreateSurfaceGradient(
            _isFocused ? "#183652" : _isHovered ? "#15273D" : "#101C2E",
            _isFocused ? "#132C44" : _isHovered ? "#193047" : "#14253A");
        _surface.BorderBrush = ResolveBrush(_isFocused ? "AccentStrongBrush" : "SurfaceStrokeBrush",
            _isFocused ? "#B7F7FF" : "#27425E");
        Opacity = _isPressed ? 0.94 : 1.0;
        _glow.Background = ResolveBrush("AccentHaloBrush", "#1B68DAFF");
        _glow.Opacity = _isFocused ? 0.12 : _isHovered ? 0.05 : 0;
        _shine.Background = _isFocused
            ? ResolveBrush("AccentStrongBrush", "#D7FBFF")
            : ResolveBrush("TextPrimaryBrush", "#F2F8FF");
        _shine.Opacity = _isFocused ? 0.3 : _isHovered ? 0.16 : 0.08;

        var displayText = _text;
        if (string.IsNullOrWhiteSpace(displayText))
        {
            _label.Text = _isFocused ? $"{_placeholderText} |" : _placeholderText;
            _label.Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6");
            return;
        }

        _label.Text = _isFocused ? $"{displayText} |" : displayText;
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

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
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
}
