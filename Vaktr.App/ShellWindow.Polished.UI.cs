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
        StartupTrace.Write("BuildRootLayout // polished-v19");
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
                    CreateBackdropGlow(380, 380, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-50, -40, 0, 0), "AccentHaloBrush", "#1B68DAFF"),
                    CreateBackdropGlow(320, 320, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 20, -40, 0), "WarningHaloBrush", "#15FF9B54"),
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
        StartupTrace.Write("BuildShellStack // polished-v19");
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
        StartupTrace.Write("BuildHeader // polished-v19");
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
                CreateSecondaryText("A local-first telemetry board with live timelines, compact gauges, and zero backend setup."),
                CreateMutedText("Defaults: Dark theme, 2 second scrape, 24 hour retention, machine-local storage.", 12),
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
                new Border
                {
                    Background = ResolveBrush("SurfaceBrush", "#102131"),
                    BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            CreateAccentText("LOCAL DB", 11, 70),
                            CreateSecondaryText("%LocalAppData%\\Vaktr\\Data", 12),
                        },
                    },
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
        StartupTrace.Write("BuildControlsSurface // polished-v19");
        var root = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Click a field to tune scrape timing, retention, or storage. Defaults stay local and safe if you leave them alone."),
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
        StartupTrace.Write("RenderControlDeckSummary // polished-v19");
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
        StartupTrace.Write("RenderEditableControlDeck // polished-v19");
        _controlDeckEditableActive = true;

        StartupTrace.Write("RenderEditableControlDeck // build collection grid");
        var fieldGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 12,
        };
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddCardToGrid(fieldGrid, 0, 0, CreateSettingFieldCard(
            "Scrape interval",
            FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds),
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput)
                ? "Default lane active"
                : "Custom lane active",
            _activeDeckEditor == DeckEditorMode.Scrape,
            OnScrapeFieldClick));
        AddCardToGrid(fieldGrid, 0, 1, CreateSettingFieldCard(
            "Max retention",
            GetRetentionFieldValue(),
            GetRetentionFieldCaption(),
            _activeDeckEditor == DeckEditorMode.Retention,
            OnRetentionFieldClick));
        AddCardToGrid(fieldGrid, 0, 2, CreateSettingFieldCard(
            "Storage path",
            GetStorageFieldTitle(),
            GetStorageFieldCaption(),
            _activeDeckEditor == DeckEditorMode.Storage,
            OnStorageFieldClick));
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
                    CreateSectionHeader("COLLECTION", "Click a field to tune scrape timing, retention, or storage without leaving the main board."),
                    fieldGrid,
                    CreateActiveDeckEditorSurface(),
                },
            },
        };

        var actionRow = new Grid
        {
            ColumnSpacing = 12,
        };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateMutedText("Storage stays machine-local by default.", 12),
                CreateSecondaryText($"{VaktrConfig.DefaultStorageDirectory} // not roaming", 12),
            },
        });

        var actionChips = CreateChipRow(
            CreateActionChip(
                _viewModel.SelectedTheme == ThemeMode.Dark ? "Dark mode" : "Light mode",
                OnThemeQuickToggle),
            CreateActionChip("Apply", OnSaveSettingsClick, filled: true));
        actionRow.Children.Add(actionChips);
        Grid.SetColumn(actionChips, 1);

        StartupTrace.Write("RenderEditableControlDeck // assign host");
        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                editorCard,
                actionRow,
                CreateMutedText("Drag on any graph to zoom into a smaller time slice. Double-click a chart to reset back out.", 12),
            },
        };
        StartupTrace.Write("RenderEditableControlDeck // complete");
    }

    private UIElement BuildSummarySurface()
    {
        StartupTrace.Write("BuildSummarySurface // polished-v19");
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
        StartupTrace.Write("BuildFooter // polished-v19");
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

    private UIElement CreateActiveDeckEditorSurface()
    {
        return _activeDeckEditor switch
        {
            DeckEditorMode.Retention => CreateRetentionEditorSurface(),
            DeckEditorMode.Storage => CreateStorageEditorSurface(),
            _ => CreateScrapeEditorSurface(),
        };
    }

    private Border CreateScrapeEditorSurface()
    {
        return CreateFocusedEditorSurface(
            "SCRAPE",
            "Pick how often Vaktr samples the local node.",
            CreateChipWrapRow(
                CreatePresetChip("-1s", false, (_, _) => NudgeScrapeInterval(-1)),
                CreatePresetChip("+1s", false, (_, _) => NudgeScrapeInterval(1)),
                CreatePresetChip("1s", _viewModel.EffectiveScrapeIntervalSeconds == 1, (_, _) => SetScrapeInterval(1)),
                CreatePresetChip("2s", string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput), (_, _) => ResetScrapeInterval()),
                CreatePresetChip("5s", _viewModel.EffectiveScrapeIntervalSeconds == 5, (_, _) => SetScrapeInterval(5)),
                CreatePresetChip("10s", _viewModel.EffectiveScrapeIntervalSeconds == 10, (_, _) => SetScrapeInterval(10)),
                CreatePresetChip("15s", _viewModel.EffectiveScrapeIntervalSeconds == 15, (_, _) => SetScrapeInterval(15)),
                CreatePresetChip("30s", _viewModel.EffectiveScrapeIntervalSeconds == 30, (_, _) => SetScrapeInterval(30)),
                CreatePresetChip("60s", _viewModel.EffectiveScrapeIntervalSeconds == 60, (_, _) => SetScrapeInterval(60))),
            "2 seconds is the default because it feels live without making the collector noisy.");
    }

    private Border CreateRetentionEditorSurface()
    {
        var retentionBox = CreateDeckInlineEntry(_viewModel.RetentionHoursInput, "24h");
        retentionBox.TextChanged += OnRetentionInputChanged;

        return CreateFocusedEditorSurface(
            "RETENTION",
            "Type retention with m, h, or d units, then keep it simple with a preset when that is faster.",
            new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            CreateFieldLabel("Retention window"),
                            retentionBox,
                            CreateMutedText("Accepted formats: 30m, 24h, 7d. Minute inputs round up to the next full hour when Vaktr saves them.", 12),
                        },
                    },
                    CreateChipWrapRow(
                        CreatePresetChip("6h", _viewModel.EffectiveRetentionHours == 6, (_, _) => SetRetentionInput("6h")),
                        CreatePresetChip("12h", _viewModel.EffectiveRetentionHours == 12, (_, _) => SetRetentionInput("12h")),
                        CreatePresetChip("24h", _viewModel.EffectiveRetentionHours == 24, (_, _) => ResetRetentionHours()),
                        CreatePresetChip("7d", _viewModel.EffectiveRetentionHours == 168, (_, _) => SetRetentionInput("7d")),
                        CreatePresetChip("30d", _viewModel.EffectiveRetentionHours == 720, (_, _) => SetRetentionInput("30d")),
                        CreatePresetChip("90d", _viewModel.EffectiveRetentionHours == 2160, (_, _) => SetRetentionInput("90d"))),
                },
            },
            "Blank keeps the smart 24h default. Custom values still compact older data into 1-minute rollups after the first 6 hours.");
    }

    private Border CreateStorageEditorSurface()
    {
        var pathText = CreatePrimaryText(GetStorageSummaryText(), 16, true);
        pathText.FontFamily = new FontFamily("Bahnschrift");

        var dropSurface = new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
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
                    CreateMutedText("Drop a folder here or browse for one. Leave it blank to use the safe local default.", 12),
                },
            },
        };
        dropSurface.DragOver += OnStoragePathDragOver;
        dropSurface.Drop += OnStoragePathDrop;

        return new Border
        {
            Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    CreateSectionHeader("STORAGE", "Vaktr keeps telemetry machine-local, so it belongs in Local App Data instead of Roaming."),
                    dropSurface,
                    CreateChipWrapRow(
                        CreateActionChip("Browse folder", OnBrowseStorageClick, filled: true),
                        CreateActionChip("Use default", (_, _) => ResetStorageDirectory())),
                    CreateSecondaryText("Default: %LocalAppData%\\Vaktr\\Data. That keeps metrics local to this Windows machine and avoids roaming-profile churn.", 12),
                },
            },
        };
    }

    private Border CreateFocusedEditorSurface(string eyebrow, string description, Panel chips, string footer)
    {
        return new Border
        {
            Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    CreateSectionHeader(eyebrow, description),
                    chips,
                    CreateSecondaryText(footer, 12),
                },
            },
        };
    }

    private Border CreateSettingFieldCard(string title, string value, string caption, bool isActive, EventHandler onClick)
    {
        var border = new Border
        {
            Background = ResolveBrush(isActive ? "SurfaceStrongBrush" : "SurfaceElevatedBrush", isActive ? "#183148" : "#15283B"),
            BorderBrush = ResolveBrush(isActive ? "AccentStrongBrush" : "SurfaceStrokeBrush", isActive ? "#B7F7FF" : "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateMutedText(title.ToUpperInvariant(), 11),
                    CreatePrimaryText(value, 18, true),
                    CreateSecondaryText(caption, 12),
                },
            },
        };

        border.PointerEntered += (_, _) =>
        {
            if (!isActive)
            {
                border.Background = ResolveBrush("SurfaceStrongBrush", "#183148");
                border.BorderBrush = ResolveBrush("AccentHaloBrush", "#1B68DAFF");
            }
        };
        border.PointerExited += (_, _) =>
        {
            if (!isActive)
            {
                border.Background = ResolveBrush("SurfaceElevatedBrush", "#15283B");
                border.BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E");
            }
        };
        border.Tapped += (_, _) => onClick(border, EventArgs.Empty);
        return border;
    }

    private static StackPanel CreateChipWrapRow(params ActionChip[] chips)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        foreach (var chip in chips)
        {
            row.Children.Add(chip);
        }

        return row;
    }

    private static ActionChip CreatePresetChip(string text, bool isActive, EventHandler onClick)
    {
        var chip = CreateActionChip(text, onClick, isActive);
        chip.IsActive = isActive;
        return chip;
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

    private static InlineTextEntry CreateDeckInlineEntry(string text, string placeholderText) =>
        new()
        {
            Text = text,
            PlaceholderText = placeholderText,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

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

    private string GetStorageFieldTitle()
    {
        return string.IsNullOrWhiteSpace(_viewModel.StorageDirectory)
            ? "Local app data"
            : "Custom folder";
    }

    private string GetStorageFieldCaption()
    {
        return string.IsNullOrWhiteSpace(_viewModel.StorageDirectory)
            ? "%LocalAppData%\\Vaktr\\Data // machine-local"
            : _viewModel.StorageDirectory.Trim();
    }

    private string GetRetentionFieldCaption()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.RetentionHoursInput))
        {
            return "Type 30m, 24h, or 7d";
        }

        return _viewModel.HasValidRetentionInput
            ? "Custom retention active"
            : "Use m, h, or d only";
    }

    private string GetRetentionFieldValue()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.RetentionHoursInput))
        {
            return MainViewModel.FormatRetentionInput(_viewModel.EffectiveRetentionHours);
        }

        return MainViewModel.TryParseRetentionInput(_viewModel.RetentionHoursInput, out _, out var normalizedText)
            ? normalizedText
            : _viewModel.RetentionHoursInput.Trim();
    }

    private static string FormatScrapeInterval(int seconds) =>
        seconds == 1 ? "1 second" : $"{seconds} seconds";

    private static string FormatRetention(int hours)
    {
        if (hours % 24 == 0)
        {
            var days = hours / 24;
            return days == 1 ? "1 day" : $"{days} days";
        }

        return hours == 1 ? "1 hour" : $"{hours} hours";
    }

    private string GetBehaviorSummaryText()
    {
        var startup = _viewModel.LaunchOnStartup ? "Launch on sign-in enabled" : "Manual launch";
        var tray = _viewModel.MinimizeToTray ? "close => tray" : "close => exit";
        return $"{startup} // {tray}";
    }
}
