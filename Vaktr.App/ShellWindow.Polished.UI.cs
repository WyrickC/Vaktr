using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Vaktr.App.ViewModels;

namespace Vaktr.App;

public sealed partial class ShellWindow
{
    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout // polished-v10");
        var shellBorder = new Border
        {
            Background = ResolveBrush("ShellBackgroundBrush", "#0B1622"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1E3144"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            Padding = new Thickness(22),
            Child = BuildShellStack(),
        };

        var root = new Grid
        {
            Background = ResolveBrush("AppBackdropBrush", "#061018"),
        };

        root.Children.Add(new ScrollViewer
        {
            Content = new Grid
            {
                Margin = new Thickness(18),
                Children =
                {
                    shellBorder,
                },
            },
        });

        root.Loaded += OnRootLoaded;
        root.SizeChanged += OnRootLayoutSizeChanged;
        return root;
    }

    private StackPanel BuildShellStack()
    {
        StartupTrace.Write("BuildShellStack // polished-v10");
        return new StackPanel
        {
            Spacing = 22,
            Children =
            {
                BuildHeader(),
                BuildControlsSurface(),
                CreateSectionHeader("AT A GLANCE", "Fast launch, low overhead, and local-only telemetry with sensible defaults."),
                BuildSummarySurface(),
                CreateSectionHeader("LIVE BOARD", "Time-series panels for CPU, memory, disk I/O, network activity, and drive-usage gauges."),
                _dashboardGrid,
                BuildFooter(),
            },
        };
    }

    private Grid BuildHeader()
    {
        StartupTrace.Write("BuildHeader // polished-v10");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateAccentText("LOCAL NODE // READY OUT OF THE BOX", 12, 110),
                CreatePrimaryText("Vaktr", 34, true),
                CreateSecondaryText("A lightweight, Grafana-inspired Windows telemetry board with live timelines, compact gauges, and no external setup."),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        CreateSignalPill("fast launch"),
                        CreateSignalPill("sqlite history"),
                        CreateSignalPill("local only"),
                    },
                },
            },
        };

        var actionsStack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                new Border
                {
                    Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                    BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12, 10, 12, 10),
                    Child = _statusText,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        CreateGhostButton("Theme", OnThemeQuickToggle),
                        CreateFilledButton("Apply", OnSaveSettingsClick),
                    },
                },
            },
        };

        grid.Children.Add(titleStack);
        grid.Children.Add(actionsStack);
        Grid.SetColumn(actionsStack, 1);
        return grid;
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // polished-v10");
        var root = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Adjust timing, retention, theme, autostart, and the panels that stay visible."),
                _controlsBodyHost,
            },
        };

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(18),
            Child = root,
        };
    }

    private void RenderStartupControls()
    {
        _controlsBodyHost.Children.Clear();
        _controlsBodyHost.Children.Add(CreatePlaceholderCard("Startup", "The fast-launch shell paints first, then the full control deck wakes up."));
        _controlsBodyHost.Children.Add(CreateSecondaryText("Theme and telemetry settings will appear after the window is alive."));
    }

    private void RenderAdvancedControls()
    {
        StartupTrace.Write("RenderAdvancedControls // polished-v10");
        _controlsBodyHost.Children.Clear();
        var controlsGrid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 14,
        };
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddStatusField(controlsGrid, 0, 0, "Scrape interval", $"{_viewModel.SelectedIntervalSeconds}s");
        AddStatusField(controlsGrid, 0, 1, "Window", $"{_viewModel.SelectedWindowMinutes}m");
        AddStatusField(controlsGrid, 0, 2, "Retention", _viewModel.SelectedRetentionDays == 0 ? "Forever" : $"{_viewModel.SelectedRetentionDays}d");
        AddStatusField(controlsGrid, 0, 3, "Theme", _viewModel.SelectedTheme.ToString());
        _controlsBodyHost.Children.Add(controlsGrid);

        _controlsBodyHost.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                CreateSignalPill(_viewModel.LaunchOnStartup ? "autostart on" : "autostart off"),
                CreateSignalPill(_viewModel.MinimizeToTray ? "tray close on" : "tray close off"),
                CreateSignalPill(string.IsNullOrWhiteSpace(_viewModel.StorageDirectory) ? "default storage" : "custom storage"),
            },
        });

        _controlsBodyHost.Children.Add(new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateMutedText("Panel visibility"),
                    CreateSecondaryText("Live panels appear automatically as the local node starts reporting telemetry."),
                },
            },
        });
    }

    private UIElement BuildSummarySurface()
    {
        StartupTrace.Write("BuildSummarySurface // polished-v10");
        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16),
            Child = _summaryHost,
        };
    }

    private Border BuildFooter()
    {
        StartupTrace.Write("BuildFooter // polished-v10");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateAccentText("LOW OVERHEAD // ALWAYS LOCAL", 11, 80),
                CreateSecondaryText("Vaktr keeps a bounded live window in memory, writes history to SQLite, and avoids heavyweight dashboard plumbing."),
            },
        });

        var tag = CreateMutedText("WinUI 3 telemetry board", 12);
        tag.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(tag);
        Grid.SetColumn(tag, 1);

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = grid,
        };
    }

    private static void AddControlField(Grid grid, int row, int column, string label, FrameworkElement control, int columnSpan = 1)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var stack = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateFieldLabel(label),
                control,
            },
        };

        grid.Children.Add(stack);
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
        Grid.SetColumnSpan(stack, columnSpan);
    }

    private static void AddStatusField(Grid grid, int row, int column, string label, string value)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var card = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateMutedText(label, 11),
                    CreatePrimaryText(value, 16, true),
                },
            },
        };

        grid.Children.Add(card);
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
    }

    private static Border CreatePlaceholderCard(string title, string text) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateMutedText(title, 11),
                    CreateSecondaryText(text, 12),
                },
            },
        };

    private static Ellipse CreateBackdropGlow(double width, double height, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, Thickness margin, string resourceKey, string fallbackHex) =>
        new()
        {
            Width = width,
            Height = height,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            Margin = margin,
            Fill = ResolveBrush(resourceKey, fallbackHex),
            Opacity = 0.2,
        };

    private static Border CreateSectionHeader(string eyebrow, string text) =>
        new()
        {
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateAccentText(eyebrow, 11, 80),
                    CreateSecondaryText(text, 13),
                },
            },
        };

    private static Border CreateSignalPill(string text) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 5, 10, 5),
            Child = CreateMutedText(text, 11),
        };

    private static ComboBox CreateSettingsComboBox(object itemsSource, string displayMemberPath, string selectedValuePath) =>
        new()
        {
            ItemsSource = itemsSource,
            DisplayMemberPath = displayMemberPath,
            SelectedValuePath = selectedValuePath,
            Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            MinWidth = 140,
        };

    private static TextBox CreateDeckTextBox() =>
        new()
        {
            Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
        };

    private static CheckBox CreateDeckCheckBox(string content) =>
        new()
        {
            Content = content,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            Margin = new Thickness(0, 4, 0, 0),
        };

    private static Binding TwoWay(string path) =>
        new()
        {
            Mode = BindingMode.TwoWay,
            Path = new PropertyPath(path),
        };

    private static Button CreateFilledButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Background = ResolveBrush("AccentSoftBrush", "#10394D"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 40,
        };
        button.Click += onClick;
        return button;
    }

    private static Button CreateGhostButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 40,
        };
        button.Click += onClick;
        return button;
    }

    private static TextBlock CreatePrimaryText(string text, double fontSize, bool semiBold) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            FontWeight = semiBold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateSecondaryText(string text, double fontSize = 14) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateMutedText(string text, double fontSize = 12) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateAccentText(string text, double fontSize, int characterSpacing) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
        };

    private static TextBlock CreateFieldLabel(string text)
    {
        return CreateMutedText(text, 12);
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
