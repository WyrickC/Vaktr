using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Vaktr.App.Controls;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App;

public sealed partial class ShellWindow
{
    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout // polished-v17");
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
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            ZoomMode = ZoomMode.Disabled,
            IsTabStop = false,
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
        StartupTrace.Write("BuildShellStack // polished-v17");
        return new StackPanel
        {
            Spacing = 24,
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
        StartupTrace.Write("BuildHeader // polished-v17");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brandRow = new Grid
        {
            ColumnSpacing = 18,
        };
        brandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        brandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        brandRow.Children.Add(_brandHost);

        var titleStack = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateAccentText("LOCAL NODE // READY OUT OF THE BOX", 12, 110),
                new Border
                {
                    Width = 148,
                    Height = 4,
                    CornerRadius = new CornerRadius(999),
                    Background = ResolveBrush("AccentBrush", "#66E7FF"),
                    Opacity = 0.88,
                },
                CreatePrimaryText("Vaktr", 34, true),
                CreateSecondaryText("A lightweight, Grafana-inspired Windows telemetry board with live timelines, compact gauges, and no external setup."),
                CreateMutedText($"Defaults to 24h of local history at {VaktrConfig.DefaultStorageDirectory}", 12),
            },
        };
        brandRow.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

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
            },
        };

        grid.Children.Add(brandRow);
        grid.Children.Add(actionsStack);
        Grid.SetColumn(actionsStack, 1);
        return grid;
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // polished-v17");
        var root = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Optional settings for scrape timing, retention, and storage. Leave fields blank to stay on the safe defaults."),
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

    private void RenderControlDeckSummary()
    {
        StartupTrace.Write("RenderControlDeckSummary // polished-v17");
        var settingsGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 12,
        };
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddStatusField(settingsGrid, 0, 0, "Scrape", $"{_viewModel.EffectiveScrapeIntervalSeconds}s");
        AddStatusField(settingsGrid, 0, 1, "Retention", $"{_viewModel.EffectiveRetentionHours}h");
        AddStatusField(settingsGrid, 0, 2, "Storage", GetStorageSummaryText());

        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                settingsGrid,
                CreateSecondaryText(GetBehaviorSummaryText(), 12),
                CreateMutedText("The deck becomes editable after startup settles. Recent samples stay full resolution for 6 hours, then older history compacts into 1-minute rollups automatically.", 12),
            },
        };
    }

    private void RenderEditableControlDeck()
    {
        StartupTrace.Write("RenderEditableControlDeck // polished-v17");
        _controlDeckEditableActive = true;

        StartupTrace.Write("RenderEditableControlDeck // build collection grid");
        var collectionGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 12,
        };
        collectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        collectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddControlField(collectionGrid, 0, 0, "Scrape interval", CreateInlineLabel("Low-latency but lightweight"));
        AddControlField(collectionGrid, 0, 1, string.Empty, CreateStepperCard(
            $"{_viewModel.EffectiveScrapeIntervalSeconds}s",
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput)
                ? "Using the default 2 second scrape."
                : "Custom interval override active.",
            (_, _) => StepScrapeInterval(-1),
            (_, _) => StepScrapeInterval(1),
            (_, _) => ResetScrapeInterval()));

        AddControlField(collectionGrid, 1, 0, "Max retention", CreateInlineLabel("Older samples compact automatically"));
        AddControlField(collectionGrid, 1, 1, string.Empty, CreateStepperCard(
            $"{_viewModel.EffectiveRetentionHours}h",
            string.IsNullOrWhiteSpace(_viewModel.RetentionHoursInput)
                ? "Using the default 24 hour retention cap."
                : "Custom retention override active.",
            (_, _) => StepRetentionHours(-1),
            (_, _) => StepRetentionHours(1),
            (_, _) => ResetRetentionHours()));

        AddControlField(collectionGrid, 2, 0, "Storage path", CreateInlineLabel("Drop a folder to override the default"));
        AddControlField(collectionGrid, 2, 1, string.Empty, CreateStorageDropCard());
        StartupTrace.Write("RenderEditableControlDeck // wrap editor");
        var editorCard = new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    CreateSectionHeader("COLLECTION", "Tune scrape timing, retention, and storage without leaving the main board."),
                    collectionGrid,
                    CreateSecondaryText("Vaktr defaults to Dark mode, a 2 second scrape interval, 24 hours of retention, and %LocalAppData%\\Vaktr\\Data storage.", 12),
                },
            },
        };

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                CreateActionChip(
                    _viewModel.SelectedTheme == ThemeMode.Dark ? "Dark mode" : "Light mode",
                    OnThemeQuickToggle),
                CreateActionChip("Apply", OnSaveSettingsClick, filled: true),
            },
        };

        StartupTrace.Write("RenderEditableControlDeck // assign host");
        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                editorCard,
                actionRow,
                CreateMutedText("Drag on any graph to zoom into a smaller time slice. Click a range chip to reset back out.", 12),
            },
        };
        StartupTrace.Write("RenderEditableControlDeck // complete");
    }

    private UIElement BuildSummarySurface()
    {
        StartupTrace.Write("BuildSummarySurface // polished-v17");
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
        StartupTrace.Write("BuildFooter // polished-v17");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateAccentText("LOW OVERHEAD // ALWAYS LOCAL", 11, 80),
                CreateSecondaryText("Vaktr keeps a bounded live window in memory, stores metrics in SQLite, and compacts older samples into lighter rollups automatically."),
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

        FrameworkElement element;
        if (string.IsNullOrWhiteSpace(label))
        {
            element = control;
        }
        else
        {
            element = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateFieldLabel(label),
                    control,
                },
            };
        }

        grid.Children.Add(element);
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private static void AddCardToGrid(Grid grid, int row, int column, FrameworkElement element, int columnSpan = 1)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        grid.Children.Add(element);
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
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

    private Border CreateStepperCard(string valueText, string caption, EventHandler decrementHandler, EventHandler incrementHandler, EventHandler resetHandler)
    {
        var valueBlock = CreatePrimaryText(valueText, 18, true);
        valueBlock.FontFamily = new FontFamily("Bahnschrift");

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(valueBlock);

        var chipRow = CreateChipRow(
            CreateActionChip("-", decrementHandler),
            CreateActionChip("+", incrementHandler, filled: true),
            CreateActionChip("Default", resetHandler));
        headerGrid.Children.Add(chipRow);
        Grid.SetColumn(chipRow, 1);

        return new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    headerGrid,
                    CreateSecondaryText(caption, 12),
                },
            },
        };
    }

    private Border CreateStorageDropCard()
    {
        var pathText = CreatePrimaryText(GetStorageSummaryText(), 15, true);
        pathText.FontFamily = new FontFamily("Bahnschrift");

        var dropSurface = new Border
        {
            Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
            BorderBrush = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            AllowDrop = true,
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    pathText,
                    CreateMutedText("Drop a folder here to change where Vaktr stores metrics. Leave it blank to use the local default.", 12),
                },
            },
        };
        dropSurface.DragOver += OnStoragePathDragOver;
        dropSurface.Drop += OnStoragePathDrop;

        return new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    dropSurface,
                    CreateChipRow(CreateActionChip("Use default", (_, _) => ResetStorageDirectory()))
                },
            },
        };
    }

    private async void OnStoragePathDrop(object sender, DragEventArgs e)
    {
        try
        {
            string? path = null;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var item = items.FirstOrDefault();
                if (item is not null)
                {
                    path = item.Path;
                }
            }
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                path = text.Trim().Trim('"');
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                StartupTrace.Write($"Storage path dropped: {path}");
                SetStorageDirectory(path);
            }
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("StoragePathDrop", ex);
            _viewModel.StatusText = $"Storage path issue: {ex.Message}";
            UpdateStatusText();
        }
    }

    private static void OnStoragePathDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private static StackPanel CreateChipRow(params ActionChip[] chips)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        foreach (var chip in chips)
        {
            stack.Children.Add(chip);
        }

        return stack;
    }

    private static ActionChip CreateActionChip(string text, EventHandler onClick, bool filled = false)
    {
        var chip = new ActionChip
        {
            Text = text,
            IsFilled = filled,
            MinHeight = 38,
        };
        chip.Click += onClick;
        return chip;
    }

    private static TextBlock CreateInlineLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 12,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static Border CreateBrandPlaceholder() =>
        new()
        {
            Width = 92,
            Height = 92,
            CornerRadius = new CornerRadius(24),
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "V",
                        FontFamily = new FontFamily("Bahnschrift"),
                        FontSize = 40,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };

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
            IsHitTestVisible = false,
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
            CharacterSpacing = characterSpacing,
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

    private string GetStorageSummaryText()
    {
        return string.IsNullOrWhiteSpace(_viewModel.StorageDirectory)
            ? "%LocalAppData%\\Vaktr\\Data"
            : _viewModel.StorageDirectory.Trim();
    }

    private string GetBehaviorSummaryText()
    {
        var startup = _viewModel.LaunchOnStartup ? "Launch on sign-in enabled" : "Manual launch";
        var tray = _viewModel.MinimizeToTray ? "close => tray" : "close => exit";
        return $"{startup} // {tray}";
    }
}
