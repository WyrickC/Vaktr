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
    private Grid BuildLoadingScreen()
    {
        // Ultra-lightweight — no resource lookups, no helper methods, renders on first frame
        var accentColor = BrushFactory.CreateBrush("#66E7FF");
        var ld1 = new Ellipse { Width = 8, Height = 8, Fill = accentColor };
        var ld2 = new Ellipse { Width = 8, Height = 8, Fill = accentColor, Opacity = 0.5 };
        var ld3 = new Ellipse { Width = 8, Height = 8, Fill = accentColor, Opacity = 0.25 };

        var loadingPulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        foreach (var (dot, delay) in new[] { (ld1, 0), (ld2, 200), (ld3, 400) })
        {
            var anim = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(delay) };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0.25 });
            anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), Value = 1.0 });
            anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)), Value = 0.25 });
            anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), Value = 0.25 });
            Storyboard.SetTarget(anim, dot);
            Storyboard.SetTargetProperty(anim, "Opacity");
            loadingPulse.Children.Add(anim);
        }

        _loadingOverlay = new Border
        {
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { ld1, ld2, ld3 },
                    },
                    new TextBlock
                    {
                        Text = "Starting Vaktr",
                        FontSize = 13,
                        Foreground = BrushFactory.CreateBrush("#7D9AB6"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };
        _loadingOverlay.Loaded += (_, _) => loadingPulse.Begin();

        var root = new Grid
        {
            Background = BrushFactory.CreateBrush("#030812"),
        };
        root.Children.Add(_loadingOverlay);

        // After this screen renders, build the full UI on the next frame
        root.Loaded += (_, _) =>
        {
            _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, BuildFullUi);
        };

        return root;
    }

    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout // polished-v19");
        var shellHalo = new Border
        {
            Margin = new Thickness(-10),
            Background = ResolveBrush("AccentHaloBrush", "#1B68DAFF"),
            CornerRadius = new CornerRadius(40),
            Opacity = 0.08,
        };

        var shellOutline = new Border
        {
            Margin = new Thickness(-1),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1A3145"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(38),
            Opacity = 0.08,
        };

        var shellBorder = new Border
        {
            Background = ResolveBrush("ShellBackgroundBrush", "#07101B"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1A3145"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(38),
            Padding = new Thickness(28, 12, 28, 26),
            Child = BuildShellStack(),
        };

        var root = new Grid
        {
            Background = ResolveBrush("AppBackdropBrush", "#030812"),
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(_titleBarDragHost);

        _scrollHost.Content = new Grid
        {
            Margin = new Thickness(24, 8, 24, 28),
            Children =
            {
                shellHalo,
                shellOutline,
                shellBorder,
            },
        };
        root.Children.Add(_scrollHost);
        Grid.SetRow(_scrollHost, 1);

        root.Loaded += OnRootLoaded;
        root.SizeChanged += OnRootLayoutSizeChanged;
        return root;
    }

    private StackPanel BuildShellStack()
    {
        StartupTrace.Write("BuildShellStack // polished-v19");
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                BuildHeader(),
                CreateSectionBand("AT A GLANCE", "Current node snapshot."),
                BuildSummarySurface(),
                CreateSectionDivider(),
                BuildBoardSectionBand(),
                _dashboardGrid,
                CreateSectionDivider(),
                BuildControlsSurface(),
            },
        };
    }

    private FrameworkElement BuildHeader()
    {
        StartupTrace.Write("BuildHeader // polished-v19");
        var heroGrid = new Grid
        {
            ColumnSpacing = 18,
            Margin = new Thickness(2, 0, 2, 0),
        };
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.Children.Add(_brandHost);

        var titleText = new TextBlock
        {
            Text = "Vaktr",
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontSize = 52,
            FontWeight = FontWeights.Light,
            CharacterSpacing = 60,
            Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        };
        var subtitleText = new TextBlock
        {
            Text = "SYSTEM TELEMETRY DASHBOARD",
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 10.5,
            CharacterSpacing = 220,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Margin = new Thickness(3, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var titleStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                titleText,
                subtitleText,
            },
        };
        heroGrid.Children.Add(titleStack);
        Grid.SetColumn(titleStack, 1);

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                heroGrid,
            },
        };
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface // polished-v19");
        var root = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSectionHeader("CONTROL DECK", "Adjust cadence, retention, and storage."),
                _controlsBodyHost,
            },
        };

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#0C1726"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Padding = new Thickness(22, 20, 22, 20),
            Child = root,
        };
    }

    private void RenderControlDeckSummary()
    {
        StartupTrace.Write("RenderControlDeckSummary // polished-v19");
        _controlDeckEditableActive = false;
        var settingsGrid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 12,
        };
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddStatusField(settingsGrid, 0, 0, "Interval", FormatScrapeInterval(_viewModel.EffectiveScrapeIntervalSeconds), "Sample cadence");
        AddStatusField(settingsGrid, 0, 1, "Retention", GetRetentionFieldValue(), "History window");
        AddStatusField(settingsGrid, 0, 2, "Storage", GetStorageFieldTitle(), GetStorageFieldCaption());

        var actionRow = new Grid
        {
            ColumnSpacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var editButton = CreateChipRow(
            CreateActionChip("Edit", OnEditSettingsClick, filled: true));
        actionRow.Children.Add(editButton);
        Grid.SetColumn(editButton, 1);

        _controlsBodyHost.Child = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                settingsGrid,
                actionRow,
            },
        };
    }

    private void RenderEditableControlDeck()
    {
        StartupTrace.Write("RenderEditableControlDeck // polished-v19");
        _controlDeckEditableActive = true;

        var scrapeBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_draftScrapeIntervalInput) ? string.Empty : _draftScrapeIntervalInput,
            "2");
        scrapeBox.TextChanged += OnScrapeInputChanged;

        var retentionBox = CreateDeckInlineEntry(_draftRetentionInput, "24h");
        retentionBox.TextChanged += OnRetentionInputChanged;

        var storageBox = CreateDeckInlineEntry(
            string.IsNullOrWhiteSpace(_draftStorageDirectory) ? string.Empty : _draftStorageDirectory.Trim(),
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
                    CreateMutedText("Leave blank for the default location.", 12),
                },
            },
        };
        storageDropSurface.DragOver += OnStoragePathDragOver;
        storageDropSurface.Drop += OnStoragePathDrop;

        var fieldGrid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 12,
        };
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddCardToGrid(fieldGrid, 0, 0, CreateControlEditorCard(
            "collection",
            "COLLECTION",
            FormatScrapeInterval(GetDraftScrapeIntervalSeconds()),
            string.IsNullOrWhiteSpace(_draftScrapeIntervalInput) ? "Live default" : "Custom cadence",
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
                            CreateMutedText("1–60 seconds. Leave blank for the default 2s.", 12),
                        },
                    },
                    CreateChipWrapRow(
                        CreatePresetChip("1s", GetDraftScrapeIntervalSeconds() == 1, (_, _) => SetScrapeInterval(1)),
                        CreatePresetChip("2s", string.IsNullOrWhiteSpace(_draftScrapeIntervalInput), (_, _) => ResetScrapeInterval()),
                        CreatePresetChip("5s", GetDraftScrapeIntervalSeconds() == 5, (_, _) => SetScrapeInterval(5)),
                        CreatePresetChip("10s", GetDraftScrapeIntervalSeconds() == 10, (_, _) => SetScrapeInterval(10)),
                        CreatePresetChip("15s", GetDraftScrapeIntervalSeconds() == 15, (_, _) => SetScrapeInterval(15))),
                },
            }));

        AddCardToGrid(fieldGrid, 0, 1, CreateControlEditorCard(
            "retention",
            "RETENTION",
            GetRetentionFieldValue(_draftRetentionInput),
            string.IsNullOrWhiteSpace(_draftRetentionInput) ? "Compact local history" : "Custom retention active",
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
                            CreateMutedText("Format: 30m, 24h, or 7d.", 12),
                        },
                    },
                    CreateChipWrapRow(
                        CreatePresetChip("6h", GetDraftRetentionHours() == 6, (_, _) => SetRetentionInput("6h")),
                        CreatePresetChip("12h", GetDraftRetentionHours() == 12, (_, _) => SetRetentionInput("12h")),
                        CreatePresetChip("24h", GetDraftRetentionHours() == 24, (_, _) => ResetRetentionHours()),
                        CreatePresetChip("7d", GetDraftRetentionHours() == 168, (_, _) => SetRetentionInput("7d")),
                        CreatePresetChip("30d", GetDraftRetentionHours() == 720, (_, _) => SetRetentionInput("30d")),
                        CreatePresetChip("90d", GetDraftRetentionHours() == 2160, (_, _) => SetRetentionInput("90d"))),
                },
            }));

        AddCardToGrid(fieldGrid, 0, 2, CreateControlEditorCard(
            "storage",
            "STORAGE PATH",
            GetStorageFieldTitle(_draftStorageDirectory),
            "Stored locally by default",
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
            ColumnSpacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actionChips = CreateChipRow(
            CreateActionChip(
                _draftThemeMode == ThemeMode.Dark ? "Light mode" : "Dark mode",
                OnThemeQuickToggle),
            CreateActionChip("Cancel", OnCancelSettingsClick),
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
            Background = ResolveBrush("SurfaceElevatedBrush", "#15263C"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(15, 13, 15, 13),
            MinHeight = 82,
            Child = new Grid
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            IconFactory.CreateTile(label, accentBrush, 42, 16),
                            new StackPanel
                            {
                                Spacing = 2,
                                VerticalAlignment = VerticalAlignment.Center,
                                Children =
                                {
                                    CreateMutedText(label, 10),
                                    CreatePrimaryText(value, 18, true),
                                    CreateSecondaryText(caption, 11),
                                },
                            },
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
                            CreateMutedText("1–60 seconds. Leave blank for the default 2s.", 12),
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
                            CreateMutedText("Format: 30m, 24h, or 7d.", 12),
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
            "Blank keeps the smart 24h default. Older samples are pruned to the exact retention window you apply.");
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
                    CreateMutedText("Browse, drop a folder, or type a path. Blank uses the default.", 12),
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
                    CreateSectionHeader("STORAGE", "Vaktr keeps telemetry local, so Local App Data is the sensible default instead of Roaming."),
                    dropSurface,
                    CreateChipWrapRow(
                        CreateActionChip("Browse folder", OnBrowseStorageClick, filled: true),
                        CreateActionChip("Use default", (_, _) => ResetStorageDirectory())),
                    CreateSecondaryText("Default: %LocalAppData%\\Vaktr\\Data", 12),
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

    private static StackPanel CreateChipRow(params FrameworkElement[] chips)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
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
            MinHeight = 36,
            MinWidth = filled ? 112 : 0,
        };
        chip.Click += onClick;
        return chip;
    }

    private ActionChip CreateGlobalRangeChip(string text, int minutes)
    {
        var chip = CreateActionChip(text, OnGlobalWindowRangeClick);
        chip.Tag = minutes;
        chip.MinWidth = 48;
        chip.MinHeight = 0;
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

    private static Grid CreateBrandPlaceholder() =>
        new()
        {
            Width = 96,
            Height = 96,
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
                    CreateAccentText(eyebrow, 12, 95),
                    CreateSecondaryText(text, 14),
                },
            },
        };

    private StackPanel BuildBoardSectionBand()
    {
        var grid = new Grid
        {
            Margin = new Thickness(4, 2, 4, 0),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                CreateAccentText("LIVE BOARD", 11, 85),
                CreateSecondaryText("Real-time telemetry panels.", 12.5),
            },
        });

        var actions = CreateChipRow(
            _globalRangeButton,
            _globalResetZoomButton);
        actions.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);

        return new StackPanel
        {
            Spacing = 0,
            Children =
            {
                grid,
                _globalRangeEditorHost,
            },
        };
    }

    private static Border CreateSectionDivider() =>
        new()
        {
            Height = 1,
            Margin = new Thickness(20, 4, 20, 4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = Color.FromArgb(0, 39, 66, 94), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(50, 39, 66, 94), Offset = 0.5 },
                    new GradientStop { Color = Color.FromArgb(0, 39, 66, 94), Offset = 1 },
                },
            },
            IsHitTestVisible = false,
        };

    private static Grid CreateSectionBand(string eyebrow, string text)
    {
        return new Grid
        {
            Margin = new Thickness(4, 2, 4, 0),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        CreateAccentText(eyebrow, 11, 85),
                        CreateSecondaryText(text, 12.5),
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
            ColumnSpacing = 11,
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.Children.Add(token);

        var labelStack = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateAccentText(eyebrow, 10, 90),
                CreatePrimaryText(headline, 17, true),
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
            Padding = new Thickness(17, 15, 17, 15),
            MinHeight = 214,
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    headerGrid,
                    CreateSecondaryText(caption, 11),
                    body,
                },
            },
        };
    }

    private static Border CreateTopStatusPill(UIElement content) =>
        new()
        {
            Background = CreateSurfaceGradient("#132438", "#18314B"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#2A4662"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 10, 16, 10),
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
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = ResolveBrush("SurfaceElevatedBrush", "#112033"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#315274"),
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
                    Margin = new Thickness(4),
                    CornerRadius = new CornerRadius(9),
                    Background = ResolveBrush("AppBackdropBrush", "#061018"),
                    BorderBrush = ResolveBrush("AccentSoftBrush", "#163B60"),
                    BorderThickness = new Thickness(1),
                },
                new Microsoft.UI.Xaml.Controls.Image
                {
                    Stretch = Stretch.Uniform,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { UriSource = new Uri(imagePath) },
                    Margin = new Thickness(7),
                },
            },
        };
        return host;
    }

    private static Border CreateInfoPanel(string title, string value) =>
        new()
        {
            Background = ResolveBrush("SurfaceElevatedBrush", "#15273C"),
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
        var label = CreateMutedText(text, 11);
        label.CharacterSpacing = 20;
        return label;
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return BrushFactory.CreateBrush(fallbackHex);
    }

    private static readonly Dictionary<string, LinearGradientBrush> _gradientCache = new(StringComparer.Ordinal);
    private static bool _lastCachedLightMode;

    private static Brush CreateSurfaceGradient(string startHex, string endHex)
    {
        var isLight = IsLightPaletteActive();

        // Invalidate cache on theme change
        if (isLight != _lastCachedLightMode)
        {
            _gradientCache.Clear();
            _lastCachedLightMode = isLight;
        }

        var cacheKey = string.Concat(startHex, "|", endHex);
        if (_gradientCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        LinearGradientBrush brush;
        if (isLight)
        {
            // Use varied light gradients based on dark surface depth for visual distinction
            var startColor = LiftToLight(startHex);
            var endColor = LiftToLight(endHex);

            brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = startColor, Offset = 0d },
                    new GradientStop { Color = endColor, Offset = 1d },
                },
            };
        }
        else
        {
            brush = new LinearGradientBrush
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

        _gradientCache[cacheKey] = brush;
        return brush;
    }

    private static Color LiftToLight(string darkHex)
    {
        // Map dark surface hex to distinct light equivalents — preserves visual depth hierarchy
        return darkHex.TrimStart('#').ToUpperInvariant() switch
        {
            "0E1B2C" => ParseColor("#F8FBFF"),  // card surface start
            "13253A" => ParseColor("#EEF5FB"),  // card surface end
            "0F1C2D" => ParseColor("#F5F9FE"),  // process/summary surface start
            "15283F" or "14263A" => ParseColor("#EBF3FA"), // process/summary surface end
            "102131" or "17304A" => ParseColor("#E8F0F8"), // badge surface
            "0E1A2B" or "13263B" => ParseColor("#F2F7FC"), // chart frame
            "101C2D" or "132438" => ParseColor("#F0F5FB"), // legend row
            "102031" or "15283E" => ParseColor("#ECF2FA"), // range shell
            "0B1726" or "12243A" => ParseColor("#F4F8FD"), // chart plot
            "102133" or "162A40" => ParseColor("#E6EFF8"), // hover surface
            "0F1B2D" or "13243A" => ParseColor("#F0F6FC"), // control editor card
            _ => ParseColor("#F6F9FD"),  // default light surface
        };
    }

    private static bool IsLightPaletteActive()
    {
        var color = ResolveColor("AppBackdropBrush", "#030812");
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance >= 170d;
    }

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return ParseColor(fallbackHex);
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
        return GetStorageFieldTitle(_viewModel.StorageDirectory);
    }

    private static string GetStorageFieldTitle(string? storageDirectory)
    {
        return string.IsNullOrWhiteSpace(storageDirectory)
            ? "Local app data"
            : "Custom folder";
    }

    private string GetStorageFieldCaption()
    {
        return GetStorageFieldCaption(_viewModel.StorageDirectory);
    }

    private static string GetStorageFieldCaption(string? storageDirectory)
    {
        if (!string.IsNullOrWhiteSpace(storageDirectory))
        {
            return storageDirectory.Trim();
        }

        try
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vaktr", "Data");
        }
        catch
        {
            return "%LocalAppData%\\Vaktr\\Data";
        }
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
        return GetRetentionFieldValue(_viewModel.RetentionHoursInput);
    }

    private string GetRetentionFieldValue(string? retentionInput)
    {
        if (string.IsNullOrWhiteSpace(retentionInput))
        {
            return MainViewModel.FormatRetentionInput(VaktrConfig.DefaultMaxRetentionHours);
        }

        return MainViewModel.TryParseRetentionInput(retentionInput, out _, out var normalizedText)
            ? normalizedText
            : retentionInput.Trim();
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
        var startup = _viewModel.LaunchOnStartup ? "Auto-start" : "Manual start";
        var tray = _viewModel.MinimizeToTray ? "Close to tray" : "Close to exit";
        return $"{startup} · {tray}";
    }

}
