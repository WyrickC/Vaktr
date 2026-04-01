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
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1A3145"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(24, 22, 24, 24),
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
                Margin = new Thickness(18, 18, 18, 24),
                Children =
                {
                    CreateBackdropGlow(420, 420, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-72, -62, 0, 0), "AccentHaloBrush", "#1B68DAFF"),
                    CreateBackdropGlow(420, 420, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, -90, 0, 0), "AccentHaloBrush", "#1258D6FF"),
                    CreateBackdropGlow(340, 340, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 28, -52, 0), "WarningHaloBrush", "#15FF9B54"),
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
                CreateSectionBand("AT A GLANCE", "Fast launch, low overhead, and local-only telemetry with sensible defaults."),
                BuildSummarySurface(),
                BuildBoardSectionBand(),
                _dashboardGrid,
                BuildFooter(),
            },
        };
    }

    private FrameworkElement BuildHeader()
    {
        StartupTrace.Write("BuildHeader // polished-v19");
        var topRail = new Grid
        {
            Margin = new Thickness(2, 0, 2, 0),
        };
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var railLead = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateMiniBrandMark(),
                CreateAccentText("LOCAL NODE / READY OUT OF THE BOX", 11, 120),
            },
        };
        topRail.Children.Add(railLead);

        var railLine = new Border
        {
            Height = 1,
            Margin = new Thickness(18, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            Opacity = 0.65,
        };
        topRail.Children.Add(railLine);
        Grid.SetColumn(railLine, 1);

        var topRailStatus = CreateTopStatusPill(_statusText);
        topRail.Children.Add(topRailStatus);
        Grid.SetColumn(topRailStatus, 2);

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
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreatePrimaryText("Vaktr", 38, true),
                CreateSecondaryText("A local-first telemetry board for this Windows machine, with live timelines, compact gauges, and zero backend setup.", 17),
                CreateSecondaryText("Defaults: dark theme, 2 second scrape, 24 hour retention, machine-local storage.", 13),
            },
        };
        brandRow.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

        var heroBorder = new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(26, 24, 26, 24),
            Child = new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new Border
                    {
                        Width = 196,
                        Height = 3,
                        CornerRadius = new CornerRadius(999),
                        Background = ResolveBrush("AccentBrush", "#66E7FF"),
                        Opacity = 0.88,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    brandRow,
                },
            },
        };

        return new StackPanel
        {
            Spacing = 18,
            Children =
            {
                topRail,
                heroBorder,
            },
        };
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // polished-v19");
        var root = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Click or type right in the deck to tune scrape timing, retention, and storage. Defaults stay local and safe if you leave them alone."),
                _controlsBodyHost,
            },
        };

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(22, 20, 22, 20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new Border
                    {
                        Width = 160,
                        Height = 3,
                        CornerRadius = new CornerRadius(999),
                        Background = ResolveBrush("AccentBrush", "#66E7FF"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Opacity = 0.82,
                    },
                    root,
                },
            },
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

        AddStatusField(settingsGrid, 0, 0, "Collection", FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds), "Default live lane");
        AddStatusField(settingsGrid, 0, 1, "Retention", GetRetentionFieldValue(), "Local rollups keep older data lighter");
        AddStatusField(settingsGrid, 0, 2, "Storage path", GetStorageFieldTitle(), GetStorageFieldCaption());

        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                settingsGrid,
                CreateSecondaryText("Vaktr keeps recent telemetry at full fidelity for 6 hours, then rolls older history into lighter 1-minute slices automatically.", 12),
                CreateMutedText(GetBehaviorSummaryText(), 12),
            },
        };
    }

    private void RenderEditableControlDeck()
    {
        StartupTrace.Write("RenderEditableControlDeck // polished-v19");
        _controlDeckEditableActive = true;

        var scrapeBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput) ? string.Empty : _viewModel.ScrapeIntervalInput,
            "2");
        scrapeBox.TextChanged += OnScrapeInputChanged;

        var retentionBox = CreateDeckInlineEntry(_viewModel.RetentionHoursInput, "24h");
        retentionBox.TextChanged += OnRetentionInputChanged;

        var storageBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_viewModel.StorageDirectory) ? string.Empty : _viewModel.StorageDirectory.Trim(),
            VaktrConfig.DefaultStorageDirectory);
        storageBox.TextChanged += OnStorageInputChanged;

        var storageDropSurface = new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            AllowDrop = true,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateFieldLabel("Storage directory"),
                    storageBox,
                    CreateMutedText("Drop a folder here, browse for one, or leave this blank to use the safe local default.", 12),
                },
            },
        };
        storageDropSurface.DragOver += OnStoragePathDragOver;
        storageDropSurface.Drop += OnStoragePathDrop;

        var fieldGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 14,
        };
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddCardToGrid(fieldGrid, 0, 0, CreateControlEditorCard(
            "COL",
            "COLLECTION",
            FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds),
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput) ? "Default lane active" : "Custom lane active",
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
                            CreateFieldLabel("Scrape interval (seconds)"),
                            scrapeBox,
                            CreateMutedText("Valid range: 1 to 60 seconds. Blank keeps the default 2 second cadence.", 12),
                        },
                    },
                    CreateChipWrapRow(
                        CreatePresetChip("1s", _viewModel.EffectiveScrapeIntervalSeconds == 1, (_, _) => SetScrapeInterval(1)),
                        CreatePresetChip("2s", string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput), (_, _) => ResetScrapeInterval()),
                        CreatePresetChip("5s", _viewModel.EffectiveScrapeIntervalSeconds == 5, (_, _) => SetScrapeInterval(5)),
                        CreatePresetChip("10s", _viewModel.EffectiveScrapeIntervalSeconds == 10, (_, _) => SetScrapeInterval(10)),
                        CreatePresetChip("15s", _viewModel.EffectiveScrapeIntervalSeconds == 15, (_, _) => SetScrapeInterval(15))),
                },
            }));

        AddCardToGrid(fieldGrid, 0, 1, CreateControlEditorCard(
            "RET",
            "RETENTION",
            GetRetentionFieldValue(),
            GetRetentionFieldCaption(),
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
                            CreateMutedText("Accepted formats: 30m, 24h, 7d. Older data compacts automatically after the first 6 hours.", 12),
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
            }));

        AddCardToGrid(fieldGrid, 0, 2, CreateControlEditorCard(
            "STO",
            "STORAGE PATH",
            GetStorageFieldTitle(),
            GetStorageFieldCaption(),
            new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    storageDropSurface,
                    CreateChipWrapRow(
                        CreateActionChip("Browse folder", OnBrowseStorageClick, filled: true),
                        CreateActionChip("Use default", (_, _) => ResetStorageDirectory())),
                },
            }));

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
                CreateMutedText($"Storage path: {VaktrConfig.DefaultStorageDirectory} // machine-local, not roaming", 12),
                CreateSecondaryText("Drag on any graph to zoom into a smaller time slice. Double-click a chart to reset back out.", 12),
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
                fieldGrid,
                actionRow,
            },
        };
        StartupTrace.Write("RenderEditableControlDeck // complete");
    }

    private UIElement BuildSummarySurface()
    {
        StartupTrace.Write("BuildSummarySurface // polished-v19");
        return new Grid
        {
            Children =
            {
                _summaryHost,
            },
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
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18, 16, 18, 16),
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

    private static void AddStatusField(Grid grid, int row, int column, string label, string value, string caption)
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
                Spacing = 6,
                Children =
                {
                    CreateAccentText(label.ToUpperInvariant(), 10, 70),
                    CreatePrimaryText(value, 18, true),
                    CreateSecondaryText(caption, 12),
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
        var scrapeBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput)
                ? string.Empty
                : _viewModel.ScrapeIntervalInput,
            "2");
        scrapeBox.TextChanged += OnScrapeInputChanged;

        return CreateFocusedEditorSurface(
            "SCRAPE",
            "Choose how often Vaktr samples the local node. Type seconds directly or snap to a preset.",
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
                            CreateFieldLabel("Scrape interval (seconds)"),
                            scrapeBox,
                            CreateMutedText("Blank keeps the default 2 second cadence. Valid range: 1 to 60 seconds.", 12),
                        },
                    },
                    CreateChipWrapRow(
                        CreatePresetChip("1s", _viewModel.EffectiveScrapeIntervalSeconds == 1, (_, _) => SetScrapeInterval(1)),
                        CreatePresetChip("2s", string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput), (_, _) => ResetScrapeInterval()),
                        CreatePresetChip("5s", _viewModel.EffectiveScrapeIntervalSeconds == 5, (_, _) => SetScrapeInterval(5)),
                        CreatePresetChip("10s", _viewModel.EffectiveScrapeIntervalSeconds == 10, (_, _) => SetScrapeInterval(10)),
                        CreatePresetChip("15s", _viewModel.EffectiveScrapeIntervalSeconds == 15, (_, _) => SetScrapeInterval(15)),
                        CreatePresetChip("30s", _viewModel.EffectiveScrapeIntervalSeconds == 30, (_, _) => SetScrapeInterval(30)),
                        CreatePresetChip("60s", _viewModel.EffectiveScrapeIntervalSeconds == 60, (_, _) => SetScrapeInterval(60))),
                },
            },
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
        var storageBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_viewModel.StorageDirectory)
                ? string.Empty
                : _viewModel.StorageDirectory.Trim(),
            VaktrConfig.DefaultStorageDirectory);
        storageBox.TextChanged += OnStorageInputChanged;

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
                    CreateFieldLabel("Storage directory"),
                    storageBox,
                    CreateMutedText("Drop a folder here, browse for one, or type a path directly. Leave it blank to use the safe local default.", 12),
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
                    CreateSectionHeader("STORAGE", "Vaktr keeps telemetry machine-local, so Local App Data is the sensible default instead of Roaming."),
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
        var token = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            BorderBrush = ResolveBrush(isActive ? "AccentStrongBrush" : "SurfaceStrokeBrush", isActive ? "#B7F7FF" : "#27425E"),
            BorderThickness = new Thickness(1),
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            Child = new TextBlock
            {
                Text = title.Length >= 3 ? title[..3].ToUpperInvariant() : title.ToUpperInvariant(),
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush(isActive ? "AccentStrongBrush" : "AccentBrush", isActive ? "#B7F7FF" : "#66E7FF"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.Children.Add(token);
        var titleStack = new StackPanel
        {
            Margin = new Thickness(12, 0, 0, 0),
            Spacing = 6,
            Children =
            {
                CreateAccentText(title.ToUpperInvariant(), 10, 70),
                CreatePrimaryText(value, 17, true),
            },
        };
        topGrid.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

        var border = new Border
        {
            Background = ResolveBrush(isActive ? "SurfaceStrongBrush" : "SurfaceElevatedBrush", isActive ? "#183148" : "#15283B"),
            BorderBrush = ResolveBrush(isActive ? "AccentStrongBrush" : "SurfaceStrokeBrush", isActive ? "#B7F7FF" : "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    topGrid,
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
            Width = 96,
            Height = 96,
            CornerRadius = new CornerRadius(28),
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
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(18, 16, 18, 16),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateAccentText(title.ToUpperInvariant(), 10, 70),
                    CreatePrimaryText(text, 16, true),
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

    private Grid BuildBoardSectionBand()
    {
        var grid = new Grid
        {
            Margin = new Thickness(4, 2, 4, 0),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateAccentText("LIVE BOARD", 11, 90),
                CreateSecondaryText("Time-series panels for CPU, memory, disk I/O, network activity, and drive-usage gauges.", 14),
            },
        });

        var actions = CreateChipRow(
            CreateActionChip(GetGlobalWindowLabel(), OnCycleWindowRangeClick),
            CreateActionChip("Reset zoom", OnResetAllZoomClick));
        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);
        return grid;
    }

    private static Grid CreateSectionBand(string eyebrow, string text)
    {
        var grid = new Grid
        {
            Margin = new Thickness(4, 2, 4, 0),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateAccentText(eyebrow, 11, 90),
                CreateSecondaryText(text, 14),
            },
        });

        var line = new Border
        {
            Height = 1,
            Margin = new Thickness(18, 14, 0, 0),
            Background = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Top,
        };
        grid.Children.Add(line);
        Grid.SetColumn(line, 1);
        return grid;
    }

    private static Border CreateControlEditorCard(string tokenText, string eyebrow, string headline, string caption, FrameworkElement body)
    {
        var token = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            Child = new TextBlock
            {
                Text = tokenText,
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var headerGrid = new Grid
        {
            ColumnSpacing = 12,
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.Children.Add(token);

        var labelStack = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateAccentText(eyebrow, 10, 70),
                CreatePrimaryText(headline, 18, true),
            },
        };
        headerGrid.Children.Add(labelStack);
        Grid.SetColumn(labelStack, 1);

        return new Border
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    headerGrid,
                    CreateSecondaryText(caption, 12),
                    body,
                },
            },
        };
    }

    private static Border CreateTopStatusPill(UIElement content) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(14, 9, 14, 9),
            Child = content,
        };

    private static Border CreateMiniBrandMark() =>
        new()
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(10),
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "V",
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

    private static Border CreateInfoPanel(string title, string value) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateAccentText(title.ToUpperInvariant(), 10, 70),
                    CreateSecondaryText(value, 12),
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

    private string GetGlobalWindowLabel() => _viewModel.SelectedWindowMinutes switch
    {
        <= 1 => "1 min",
        <= 5 => "5 min",
        <= 15 => "15 min",
        _ => "1 hour",
    };
}
