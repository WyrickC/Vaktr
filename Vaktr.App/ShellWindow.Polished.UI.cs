using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;
using Vaktr.App.Controls;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;

namespace Vaktr.App;

public sealed partial class ShellWindow
{
    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout // polished-v19");
        var shellHalo = new Border
        {
            Margin = new Thickness(-8),
            Background = ResolveBrush("AccentSoftBrush", "#10394D"),
            CornerRadius = new CornerRadius(42),
            Opacity = 0.24,
        };

        var shellOutline = new Border
        {
            Margin = new Thickness(-1),
            BorderBrush = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(38),
            Opacity = 0.18,
        };

        var shellBorder = new Border
        {
            Background = CreateSurfaceGradient("#09111E", "#0E1A2B"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1A3145"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(38),
            Padding = new Thickness(28, 22, 28, 30),
            Child = BuildShellStack(),
        };

        var root = new Grid
        {
            Background = CreateSurfaceGradient("#040911", "#09111D"),
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
                Margin = new Thickness(24, 22, 24, 28),
                Children =
                {
                    shellHalo,
                    shellOutline,
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
            Spacing = 20,
            Children =
            {
                BuildHeader(),
                CreateSectionBand("AT A GLANCE", "Fast launch, low overhead, and local-only telemetry with sensible defaults."),
                BuildSummarySurface(),
                BuildBoardSectionBand(),
                _dashboardGrid,
                BuildControlsSurface(),
            },
        };
    }

    private FrameworkElement BuildHeader()
    {
        StartupTrace.Write("BuildHeader // polished-v19");
        var topRail = new Grid
        {
            Margin = new Thickness(2, 0, 2, 2),
        };
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        topRail.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateMiniBrandMark(),
            },
        });

        var railLine = new Border
        {
            Height = 1,
            Margin = new Thickness(18, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            Opacity = 0.4,
        };
        topRail.Children.Add(railLine);
        Grid.SetColumn(railLine, 1);

        var railGlow = new Border
        {
            Width = 128,
            Height = 2,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = CreateLineGradient("#4EDBFF", "#A6F6FF"),
            Opacity = 0.7,
        };
        topRail.Children.Add(railGlow);
        Grid.SetColumn(railGlow, 1);

        var topRailStatus = CreateTopStatusPill(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                _statusText,
                new TextBlock
                {
                    Text = "\u203A",
                    FontFamily = new FontFamily("Segoe UI Variable Display"),
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        });
        topRail.Children.Add(topRailStatus);
        Grid.SetColumn(topRailStatus, 2);

        var heroGrid = new Grid
        {
            ColumnSpacing = 28,
            Margin = new Thickness(2, 12, 2, 0),
        };
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.Children.Add(_brandHost);

        var titleStack = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreatePrimaryText("Vaktr", 60, true),
                CreateSecondaryText("Local-first Windows telemetry with live timelines, compact gauges, and no backend setup.", 20),
                CreateMutedText("Local defaults: 2 second scrape, 24 hour retention, machine-local storage.", 14),
            },
        };
        heroGrid.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

        var heroSurface = new Grid
        {
            Margin = new Thickness(4, 2, 4, 0),
            Children =
            {
                new Border
                {
                    Height = 1,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, -1),
                    Background = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    Opacity = 0.42,
                },
                new StackPanel
                {
                    Spacing = 10,
                    Margin = new Thickness(0, 0, 0, 10),
                    Children =
                    {
                        new Border
                        {
                            Width = 172,
                            Height = 2,
                            CornerRadius = new CornerRadius(1),
                            Background = CreateLineGradient("#4EDBFF", "#8FF0FF"),
                            Opacity = 0.76,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        heroGrid,
                    },
                }
            },
        };

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                topRail,
                heroSurface,
            },
        };
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // polished-v19");
        var root = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Tune scrape timing, retention, and storage without leaving the board."),
                _controlsBodyHost,
            },
        };

        return new Border
        {
            Background = CreateSurfaceGradient("#0D1828", "#112236"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Padding = new Thickness(22, 18, 22, 20),
            Child = new Grid
            {
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Border
                            {
                                Width = 110,
                                Height = 2,
                                CornerRadius = new CornerRadius(1),
                                Background = CreateLineGradient("#4EDBFF", "#8FF0FF"),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = 0.72,
                            },
                            root,
                        },
                    }
                },
            },
        };
    }

    private void RenderControlDeckSummary()
    {
        StartupTrace.Write("RenderControlDeckSummary // polished-v19");
        var settingsGrid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 12,
        };
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddStatusField(settingsGrid, 0, 0, "Collection", FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds), "Live sample cadence");
        AddStatusField(settingsGrid, 0, 1, "Retention", GetRetentionFieldValue(), "Local rollups stay lean");
        AddStatusField(settingsGrid, 0, 2, "Storage path", GetStorageFieldTitle(), GetStorageFieldCaption());

        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                settingsGrid,
                CreateMutedText("Vaktr keeps history local and automatically compacts older samples after the first 6 hours.", 12),
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
            Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            AllowDrop = true,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateFieldLabel("Storage directory"),
                    storageBox,
                    CreateMutedText("Drop a folder here, browse for one, or leave this blank to keep the safe local default.", 12),
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
            "collection",
            "COLLECTION",
            FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds),
            string.IsNullOrWhiteSpace(_viewModel.ScrapeIntervalInput) ? "Live default" : "Custom cadence",
            "#5DE6FF",
            new StackPanel
            {
                Spacing = 14,
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
            "retention",
            "RETENTION",
            GetRetentionFieldValue(),
            "Compact local history",
            "#67B7FF",
            new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            CreateFieldLabel("Retention window"),
                            retentionBox,
                            CreateMutedText("Use m, h, or d only. Example: 30m, 24h, 7d.", 12),
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
            "storage",
            "STORAGE PATH",
            GetStorageFieldTitle(),
            "Machine-local default",
            "#6EE7C8",
            new StackPanel
            {
                Spacing = 14,
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
            VerticalAlignment = VerticalAlignment.Center,
        };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionRow.Children.Add(new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateSecondaryText($"Default: {VaktrConfig.DefaultStorageDirectory} // drag charts to zoom, double-click to reset.", 12),
            },
        });

        var actionChips = CreateChipRow(
            CreateActionChip(
                _viewModel.SelectedTheme == ThemeMode.Dark ? "Light mode" : "Dark mode",
                OnThemeQuickToggle),
            CreateActionChip("Apply", OnSaveSettingsClick, filled: true));
        actionRow.Children.Add(actionChips);
        Grid.SetColumn(actionChips, 1);

        StartupTrace.Write("RenderEditableControlDeck // assign host");
        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 16,
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

        var accentHex = label switch
        {
            "Collection" => "#5DE6FF",
            "Retention" => "#67B7FF",
            "Storage path" => "#6EE7C8",
            _ => "#66E7FF",
        };

        var accentBrush = BrushFactory.CreateBrush(accentHex);
        var card = new Border
        {
            Background = CreateSurfaceGradient("#101C2E", "#15263C"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 15, 16, 15),
            MinHeight = 92,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    IconFactory.CreateTile(label, accentBrush, 46, 18),
                    new StackPanel
                    {
                        Spacing = 4,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            CreateMutedText(label, 11),
                            CreatePrimaryText(value, 19, true),
                            CreateSecondaryText(caption, 12),
                        },
                    },
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
            MinWidth = filled ? 118 : 0,
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
            Width = 148,
            Height = 148,
            CornerRadius = new CornerRadius(44),
            Background = CreateSurfaceGradient("#0F2032", "#17304A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        CornerRadius = new CornerRadius(30),
                        Background = ResolveBrush("AppBackdropBrush", "#061018"),
                        BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                        BorderThickness = new Thickness(1),
                    },
                    new TextBlock
                    {
                        Text = "V",
                        FontFamily = new FontFamily("Segoe UI Variable Display"),
                        FontSize = 54,
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
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(20, 18, 20, 18),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    CreateAccentText(title.ToUpperInvariant(), 10, 80),
                    CreatePrimaryText(text, 18, true),
                },
            },
        };

    private static Border CreateSectionHeader(string eyebrow, string text) =>
        new()
        {
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateAccentText(eyebrow, 13, 110),
                    CreateSecondaryText(text, 15),
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
            Spacing = 6,
            Children =
            {
                CreateAccentText("LIVE BOARD", 12, 95),
                CreateSecondaryText("Time-series panels for CPU, memory, disk I/O, network activity, and drive-usage gauges.", 15),
            },
        });

        var actions = CreateChipRow(
            CreateActionChip(GetGlobalWindowLabel(), OnCycleWindowRangeClick, true),
            CreateActionChip("Reset zoom", OnResetAllZoomClick));
        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);
        return grid;
    }

    private static Grid CreateSectionBand(string eyebrow, string text)
    {
        return new Grid
        {
            Margin = new Thickness(4, 4, 4, 2),
            Children =
            {
                new StackPanel
                {
                    Spacing = 5,
                    Children =
                    {
                        CreateAccentText(eyebrow, 12, 105),
                        CreateSecondaryText(text, 14),
                    },
                },
            },
        };
    }

    private static Border CreateControlEditorCard(string iconKey, string eyebrow, string headline, string caption, string accentHex, FrameworkElement body)
    {
        var accentBrush = BrushFactory.CreateBrush(accentHex);
        var token = IconFactory.CreateTile(iconKey, accentBrush, 50, 18);

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
                CreateAccentText(eyebrow, 10, 90),
                CreatePrimaryText(headline, 18, true),
            },
        };
        headerGrid.Children.Add(labelStack);
        Grid.SetColumn(labelStack, 1);

        return new Border
        {
            Background = CreateSurfaceGradient("#0F1B2D", "#13243A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(18, 16, 18, 16),
            MinHeight = 228,
            Child = new Grid
            {
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Border
                            {
                                Width = 76,
                                Height = 1.5,
                                CornerRadius = new CornerRadius(1),
                                Background = CreateLineGradient(accentHex, "#B5F4FF"),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = 0.78,
                            },
                            headerGrid,
                            CreateSecondaryText(caption, 12),
                            body,
                        },
                    }
                },
            },
        };
    }

    private static Border CreateTopStatusPill(UIElement content) =>
        new()
        {
            Background = CreateSurfaceGradient("#102031", "#152840"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 11, 18, 11),
            Child = new Grid
            {
                Children =
                {
                    content,
                },
            },
        };

    private static Border CreateMiniBrandMark()
    {
        var host = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(16),
            Background = CreateSurfaceGradient("#102031", "#17304A"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
        };

        var imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "vaktr.png");
        if (!File.Exists(imagePath))
        {
            host.Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "V",
                        FontFamily = new FontFamily("Segoe UI Variable Display"),
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            };
            return host;
        }

        host.Padding = new Thickness(5);
        host.Child = new Grid
        {
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = ResolveBrush("AppBackdropBrush", "#061018"),
                    BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    BorderThickness = new Thickness(1),
                },
                new Microsoft.UI.Xaml.Controls.Image
                {
                    Stretch = Stretch.Uniform,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { UriSource = new Uri(imagePath) },
                    Margin = new Thickness(3),
                },
            },
        };
        return host;
    }

    private static Border CreateInfoPanel(string title, string value) =>
        new()
        {
            Background = CreateSurfaceGradient("#101E31", "#15273C"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 11, 14, 11),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    CreateMutedText(title, 11),
                    CreatePrimaryText(value, 13, true),
                },
            },
        };

    private static TextBlock CreatePrimaryText(string text, double fontSize, bool semiBold) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily(fontSize >= 28 ? "Segoe UI Variable Display" : "Segoe UI Variable Text"),
            FontSize = fontSize,
            FontWeight = semiBold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateSecondaryText(string text, double fontSize = 14) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = fontSize,
            Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateMutedText(string text, double fontSize = 12) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = fontSize,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateAccentText(string text, double fontSize, int characterSpacing) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontWeight = FontWeights.Medium,
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

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = ParseColor(startHex), Offset = 0d },
                new GradientStop { Color = ParseColor(endHex), Offset = 1d },
            },
        };
    }

    private static Brush CreateLineGradient(string startHex, string endHex)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0.5),
            EndPoint = new Windows.Foundation.Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = ParseColor(startHex), Offset = 0d },
                new GradientStop { Color = ParseColor(endHex), Offset = 1d },
            },
        };
    }

    private static Color ParseColor(string hex)
    {
        var normalized = hex.Trim().TrimStart('#');
        return normalized.Length switch
        {
            6 => Color.FromArgb(255,
                Convert.ToByte(normalized[..2], 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16)),
            8 => Color.FromArgb(
                Convert.ToByte(normalized[..2], 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16),
                Convert.ToByte(normalized.Substring(6, 2), 16)),
            _ => Color.FromArgb(0, 0, 0, 0),
        };
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
