using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Vaktr.App.ViewModels;

namespace Vaktr.App.Controls;

public sealed class InlineTextEntry : UserControl
{
    private readonly Border _surface;
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
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 14,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _surface = new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 12, 14, 12),
            MinHeight = 46,
            Child = _label,
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
        _surface.Background = ResolveBrush(_isFocused ? "SurfaceStrongBrush" : _isHovered ? "SurfaceStrongBrush" : "SurfaceBrush",
            _isFocused || _isHovered ? "#183148" : "#102131");
        _surface.BorderBrush = ResolveBrush(_isFocused ? "AccentStrongBrush" : "SurfaceStrokeBrush",
            _isFocused ? "#B7F7FF" : "#27425E");
        Opacity = _isPressed ? 0.94 : 1.0;

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
}
