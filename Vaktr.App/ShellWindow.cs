using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Vaktr.App.Controls;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;
using Windows.Graphics;

namespace Vaktr.App;

public sealed class ShellWindow : Window
{
    private readonly MainViewModel _viewModel;
    private CollectorService? _collectorService;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private TrayIconHost? _trayIcon;

    private readonly Grid _rootLayout;
    private readonly StackPanel _summaryHost;
    private readonly Grid _dashboardGrid;
    private readonly StackPanel _panelToggleHost;
    private readonly Grid _expandedOverlay;
    private readonly MetricDeck _expandedDeck;

    private bool _allowClose;
    private bool _hasShownTrayHint;
    private bool _initialized;

    public ShellWindow(
        MainViewModel viewModel,
        IMetricStore metricStore,
        IConfigStore configStore,
        AutoLaunchService autoLaunchService)
    {
        StartupTrace.Write("ShellWindow ctor start");
        _viewModel = viewModel;
        _metricStore = metricStore;
        _configStore = configStore;
        _autoLaunchService = autoLaunchService;

        Title = "Vaktr";

        _summaryHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 18,
        };

        _dashboardGrid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 18,
        };

        _panelToggleHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };

        _expandedDeck = new MetricDeck
        {
            IsExpandedView = true,
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        StartupTrace.Write("ShellWindow core controls created");

        _expandedOverlay = BuildExpandedOverlay();
        _rootLayout = BuildRootLayout();
        _rootLayout.DataContext = _viewModel;
        StartupTrace.Write("Root layout built");
        Content = _rootLayout;
        StartupTrace.Write("Window content assigned");

        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged += OnPanelTogglesChanged;
        SubscribeToPanels(_viewModel.DashboardPanels);

        Closed += OnWindowClosed;

        BuildSummaryCards();
        RefreshPanelToggles();
        RefreshDashboardPanels();
        ApplyTheme(_viewModel.SelectedTheme);
        StartupTrace.Write("ShellWindow ctor complete");
    }

    public void ApplyTheme(ThemeMode mode)
    {
        _rootLayout.RequestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    private Grid BuildRootLayout()
    {
        StartupTrace.Write("BuildRootLayout");
        var shellBorder = new Border
        {
            Background = ResolveBrush("ShellBackgroundBrush", "#0B1622"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1E3144"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(24),
            Child = BuildShellStack(),
        };

        var root = new Grid
        {
            Background = ResolveBrush("AppBackdropBrush", "#061018"),
        };

        root.Children.Add(CreateBackdropGlow(760, 760, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-220, -280, 0, 0), "AccentHaloBrush", "#1B68DAFF"));
        root.Children.Add(CreateBackdropGlow(620, 620, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -160, -220), "WarningHaloBrush", "#15FF9B54"));
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
        root.Children.Add(_expandedOverlay);

        root.Loaded += OnRootLoaded;
        root.SizeChanged += (_, _) => RefreshDashboardPanels();
        return root;
    }

    private StackPanel BuildShellStack()
    {
        StartupTrace.Write("BuildShellStack");
        var stack = new StackPanel
        {
            Spacing = 24,
        };

        stack.Children.Add(BuildHeader());
        stack.Children.Add(BuildControlsSurface());
        stack.Children.Add(BuildSummaryStrip());
        stack.Children.Add(CreateSectionHeader("LIVE BOARD", "CPU, memory, disk, and network panels stay compact by default and can be expanded on demand."));
        stack.Children.Add(_dashboardGrid);
        stack.Children.Add(BuildFooter());
        return stack;
    }

    private Grid BuildHeader()
    {
        StartupTrace.Write("BuildHeader");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateAccentText("RIG TELEMETRY // LOCAL ONLY", 12, 120),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16,
                    Children =
                    {
                        new Border
                        {
                            Width = 54,
                            Height = 54,
                            CornerRadius = new CornerRadius(18),
                            Background = ResolveBrush("AccentSoftBrush", "#10394D"),
                            Child = new TextBlock
                            {
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                FontFamily = new FontFamily("Bahnschrift"),
                                FontSize = 26,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = ResolveBrush("AccentStrongBrush", "#B7F7FF"),
                                Text = "V",
                            },
                        },
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                CreatePrimaryText("Vaktr", 32, true),
                                CreateSecondaryText("Sleek, local, gamer-friendly hardware watch with a durable history trail."),
                            },
                        },
                    },
                },
            },
        });

        var statusText = CreateSecondaryText(string.Empty, 12);
        statusText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(MainViewModel.StatusText)) });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 10,
            Children =
            {
                new Border
                {
                    Background = ResolveBrush("SurfaceElevatedBrush", "#15283B"),
                    BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(14, 10, 14, 10),
                    Child = statusText,
                },
                CreateGhostButton("Theme", OnThemeQuickToggle),
                CreateFilledButton("Apply", OnSaveSettingsClick),
            },
        };

        grid.Children.Add(actions);
        Grid.SetColumn(actions, 1);
        return grid;
    }

    private Border BuildControlsSurface()
    {
        StartupTrace.Write("BuildControlsSurface");
        var root = new StackPanel
        {
            Spacing = 14,
        };

        root.Children.Add(CreateSectionHeader("CONTROL DECK", "Tune collection, theme, retention, startup, and the panels that stay on deck."));

        var intervalCombo = CreateSettingsComboBox(_viewModel.IntervalOptions, nameof(SelectionOption.Label), nameof(SelectionOption.Value));
        intervalCombo.SetBinding(ComboBox.SelectedValueProperty, TwoWay(nameof(MainViewModel.SelectedIntervalSeconds)));

        var windowCombo = CreateSettingsComboBox(_viewModel.WindowOptions, nameof(SelectionOption.Label), nameof(SelectionOption.Value));
        windowCombo.SetBinding(ComboBox.SelectedValueProperty, TwoWay(nameof(MainViewModel.SelectedWindowMinutes)));

        var retentionCombo = CreateSettingsComboBox(_viewModel.RetentionOptions, nameof(SelectionOption.Label), nameof(SelectionOption.Value));
        retentionCombo.SetBinding(ComboBox.SelectedValueProperty, TwoWay(nameof(MainViewModel.SelectedRetentionDays)));

        var themeCombo = CreateSettingsComboBox(_viewModel.ThemeOptions, nameof(ThemeSelectionOption.Label), nameof(ThemeSelectionOption.Value));
        themeCombo.SetBinding(ComboBox.SelectedValueProperty, TwoWay(nameof(MainViewModel.SelectedTheme)));

        var storageBox = CreateDeckTextBox();
        storageBox.SetBinding(TextBox.TextProperty, new Binding
        {
            Mode = BindingMode.TwoWay,
            Path = new PropertyPath(nameof(MainViewModel.StorageDirectory)),
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        var launchCheckBox = CreateDeckCheckBox("Launch Vaktr when Windows starts");
        launchCheckBox.SetBinding(ToggleButton.IsCheckedProperty, TwoWay(nameof(MainViewModel.LaunchOnStartup)));

        var minimizeCheckBox = CreateDeckCheckBox("Minimize to tray when closing");
        minimizeCheckBox.SetBinding(ToggleButton.IsCheckedProperty, TwoWay(nameof(MainViewModel.MinimizeToTray)));

        var grid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 14,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddControlField(grid, 0, 0, "Scrape interval", intervalCombo);
        AddControlField(grid, 0, 1, "Graph window", windowCombo);
        AddControlField(grid, 0, 2, "Retention", retentionCombo);
        AddControlField(grid, 1, 0, "Theme", themeCombo);
        AddControlField(grid, 1, 1, "Storage path", storageBox, 2);

        root.Children.Add(grid);
        root.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 18,
            Children =
            {
                launchCheckBox,
                minimizeCheckBox,
            },
        });
        root.Children.Add(CreateFieldLabel("Visible panels"));
        root.Children.Add(_panelToggleHost);

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(18),
            Child = root,
        };
    }

    private UIElement BuildSummaryStrip()
    {
        StartupTrace.Write("BuildSummaryStrip");
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = _summaryHost,
        };
    }

    private Grid BuildExpandedOverlay()
    {
        StartupTrace.Write("BuildExpandedOverlay");
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(CreateSectionHeader("EXPANDED PANEL", "Focused telemetry view for deeper reads."));

        var closeButton = CreateGhostButton("Close", OnExpandedCloseClick);
        header.Children.Add(closeButton);
        Grid.SetColumn(closeButton, 1);

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.Children.Add(header);
        contentGrid.Children.Add(_expandedDeck);
        Grid.SetRow(_expandedDeck, 1);

        var overlay = new Grid
        {
            Visibility = Visibility.Collapsed,
            Background = ResolveBrush("OverlayScrimBrush", "#A0060C14"),
        };
        overlay.Children.Add(new Border
        {
            Width = 1040,
            Height = 680,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = ResolveBrush("ShellBackgroundBrush", "#0B1622"),
            BorderBrush = ResolveBrush("ShellStrokeBrush", "#1E3144"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Padding = new Thickness(18),
            Child = contentGrid,
        });

        return overlay;
    }

    private Border BuildFooter()
    {
        StartupTrace.Write("BuildFooter");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateAccentText("LOCAL-FIRST // LOW OVERHEAD", 11, 100),
                CreateSecondaryText("Vaktr only samples local machine telemetry and writes a lightweight SQLite history."),
            },
        });

        var tag = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
            Text = "WinUI 3 dashboard",
        };
        grid.Children.Add(tag);
        Grid.SetColumn(tag, 1);

        return new Border
        {
            Background = ResolveBrush("SurfaceBrush", "#102131"),
            BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16, 14, 16, 14),
            Child = grid,
        };
    }

    private void BuildSummaryCards()
    {
        StartupTrace.Write("BuildSummaryCards");
        _summaryHost.Children.Clear();
        foreach (var card in _viewModel.SummaryCards)
        {
            var glyphText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };
            glyphText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Glyph)) });
            glyphText.SetBinding(TextBlock.ForegroundProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.AccentBrush)) });

            var valueText = CreatePrimaryText(string.Empty, 28, true);
            valueText.FontFamily = new FontFamily("Bahnschrift");
            valueText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Value)) });

            var captionText = CreateSecondaryText(string.Empty, 12);
            captionText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Caption)) });

            var accentGlow = new Border
            {
                CornerRadius = new CornerRadius(18),
                Opacity = 0.22,
            };
            accentGlow.SetBinding(Border.BackgroundProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.AccentBrush)) });

            var titleText = CreateMutedText(string.Empty, 11);
            titleText.CharacterSpacing = 50;
            titleText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(SummaryCardViewModel.Title)) });

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.Children.Add(new Grid
            {
                Width = 58,
                Height = 58,
                Children =
                {
                    accentGlow,
                    glyphText,
                },
            });

            var detailStack = new StackPanel
            {
                Margin = new Thickness(16, 0, 0, 0),
                Spacing = 4,
                Children =
                {
                    titleText,
                    valueText,
                    captionText,
                },
            };
            cardGrid.Children.Add(detailStack);
            Grid.SetColumn(detailStack, 1);

            _summaryHost.Children.Add(new Border
            {
                Width = 290,
                Background = ResolveBrush("SurfaceBrush", "#102131"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(18),
                DataContext = card,
                Child = cardGrid,
            });
        }
    }

    private void RefreshPanelToggles()
    {
        StartupTrace.Write("RefreshPanelToggles");
        _panelToggleHost.Children.Clear();
        foreach (var toggle in _viewModel.PanelToggles)
        {
            var checkBox = CreateDeckCheckBox(toggle.Title);
            checkBox.DataContext = toggle;
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, TwoWay(nameof(PanelToggleViewModel.IsVisible)));
            _panelToggleHost.Children.Add(checkBox);
        }
    }

    private void RefreshDashboardPanels()
    {
        StartupTrace.Write("RefreshDashboardPanels");
        _dashboardGrid.Children.Clear();
        _dashboardGrid.RowDefinitions.Clear();
        _dashboardGrid.ColumnDefinitions.Clear();

        var panels = _viewModel.DashboardPanels.ToArray();
        if (panels.Length == 0)
        {
            _dashboardGrid.Children.Add(new Border
            {
                Background = ResolveBrush("SurfaceBrush", "#102131"),
                BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(24),
                Child = CreateSecondaryText("Waiting for panels to come online."),
            });
            return;
        }

        var width = _rootLayout.ActualWidth > 0 ? _rootLayout.ActualWidth : 1280;
        var columns = width >= 1700 ? 3 : width >= 1100 ? 2 : 1;
        for (var column = 0; column < columns; column++)
        {
            _dashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rows = (int)Math.Ceiling(panels.Length / (double)columns);
        for (var row = 0; row < rows; row++)
        {
            _dashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < panels.Length; index++)
        {
            var deck = new MetricDeck
            {
                Panel = panels[index],
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _dashboardGrid.Children.Add(deck);
            Grid.SetColumn(deck, index % columns);
            Grid.SetRow(deck, index / columns);
        }
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        StartupTrace.Write("OnRootLoaded start");
        try
        {
            _viewModel.StatusText = "Loading local history";
            var config = _viewModel.BuildConfig();
            await _metricStore.InitializeAsync(config, CancellationToken.None);

            var historyWindow = TimeSpan.FromMinutes(Math.Max(config.GraphWindowMinutes, 5));
            var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.Subtract(historyWindow), CancellationToken.None);
            _viewModel.LoadHistory(history);

            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);

            _collectorService ??= new CollectorService(new WindowsMetricCollector(), _metricStore);
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
            _collectorService.SnapshotCollected += OnSnapshotCollected;

            _viewModel.StatusText = "Starting telemetry";
            await _collectorService.StartAsync(config, CancellationToken.None);
            _viewModel.StatusText = "Streaming local telemetry";
            StartupTrace.Write("OnRootLoaded complete");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("OnRootLoaded", ex);
            _viewModel.StatusText = $"Startup issue: {ex.Message}";
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_collectorService is not null)
        {
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
        }

        _viewModel.DashboardPanels.CollectionChanged -= OnDashboardPanelsChanged;
        _viewModel.PanelToggles.CollectionChanged -= OnPanelTogglesChanged;
        UnsubscribeFromPanels(_viewModel.DashboardPanels);
        _trayIcon?.Dispose();
        if (_collectorService is not null)
        {
            await _collectorService.DisposeAsync();
        }
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.StatusText = "Applying settings";
            _viewModel.ApplyPanelVisibility();
            var config = _viewModel.BuildConfig();
            App.CurrentApp.ApplyTheme(config.Theme);
            _autoLaunchService.SetEnabled(config.LaunchOnStartup);
            await _configStore.SaveAsync(config, CancellationToken.None);
            _collectorService ??= new CollectorService(new WindowsMetricCollector(), _metricStore);
            _collectorService.SnapshotCollected -= OnSnapshotCollected;
            _collectorService.SnapshotCollected += OnSnapshotCollected;
            await _collectorService.StartAsync(config, CancellationToken.None);
            _viewModel.StatusText = "Settings applied";
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Settings issue: {ex.Message}";
        }
    }

    private void OnThemeQuickToggle(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedTheme = _viewModel.SelectedTheme == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;

        App.CurrentApp.ApplyTheme(_viewModel.SelectedTheme);
    }

    private void OnExpandedCloseClick(object sender, RoutedEventArgs e)
    {
        _expandedOverlay.Visibility = Visibility.Collapsed;
        _expandedDeck.Panel = null;
        _viewModel.ExpandedPanel = null;
    }

    private void OnPanelExpandRequested(object? sender, EventArgs e)
    {
        if (sender is not MetricPanelViewModel panel)
        {
            return;
        }

        _viewModel.ExpandedPanel = panel;
        _expandedDeck.Panel = panel;
        _expandedOverlay.Visibility = Visibility.Visible;
    }

    private void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        _ = DispatcherQueue.TryEnqueue(() => _viewModel.ApplySnapshot(snapshot));
    }

    private void HideToTray()
    {
        EnsureTrayIcon();
        AppWindow.Hide();
        if (_hasShownTrayHint)
        {
            return;
        }

        _trayIcon?.ShowInfo("Vaktr is still running", "Use the tray icon to reopen the dashboard.");
        _hasShownTrayHint = true;
    }

    private void RestoreFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void OnDashboardPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            UnsubscribeFromPanels(e.OldItems.Cast<MetricPanelViewModel>());
        }

        if (e.NewItems is not null)
        {
            SubscribeToPanels(e.NewItems.Cast<MetricPanelViewModel>());
        }

        RefreshDashboardPanels();
    }

    private void OnPanelTogglesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshPanelToggles();

    private void SubscribeToPanels(IEnumerable<MetricPanelViewModel> panels)
    {
        foreach (var panel in panels)
        {
            panel.ExpandRequested += OnPanelExpandRequested;
        }
    }

    private void UnsubscribeFromPanels(IEnumerable<MetricPanelViewModel> panels)
    {
        foreach (var panel in panels)
        {
            panel.ExpandRequested -= OnPanelExpandRequested;
        }
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIcon = new TrayIconHost(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _trayIcon.OpenRequested += (_, _) => DispatcherQueue.TryEnqueue(RestoreFromTray);
        _trayIcon.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(RestoreFromTray);
        _trayIcon.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            _allowClose = true;
            Close();
        });
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

    private static Ellipse CreateBackdropGlow(double width, double height, HorizontalAlignment hAlign, VerticalAlignment vAlign, Thickness margin, string resourceKey, string fallbackHex) =>
        new()
        {
            Width = width,
            Height = height,
            HorizontalAlignment = hAlign,
            VerticalAlignment = vAlign,
            Margin = margin,
            Fill = ResolveBrush(resourceKey, fallbackHex),
        };

    private static Binding TwoWay(string path) => new()
    {
        Mode = BindingMode.TwoWay,
        Path = new PropertyPath(path),
    };

    private static Border CreateSectionHeader(string eyebrow, string text)
    {
        return new Border
        {
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateAccentText(eyebrow, 12, 120),
                    CreateSecondaryText(text, 14),
                },
            },
        };
    }

    private static ComboBox CreateSettingsComboBox(object itemsSource, string displayMemberPath, string selectedValuePath) => new()
    {
        ItemsSource = itemsSource,
        DisplayMemberPath = displayMemberPath,
        SelectedValuePath = selectedValuePath,
        Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
        Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
        BorderThickness = new Thickness(1),
    };

    private static TextBox CreateDeckTextBox() => new()
    {
        Background = ResolveBrush("SurfaceStrongBrush", "#183148"),
        Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        BorderBrush = ResolveBrush("SurfaceStrokeBrush", "#27425E"),
        BorderThickness = new Thickness(1),
    };

    private static CheckBox CreateDeckCheckBox(string content) => new()
    {
        Content = content,
        Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        Margin = new Thickness(0, 4, 0, 0),
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
            MinHeight = 42,
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
            MinHeight = 42,
        };
        button.Click += onClick;
        return button;
    }

    private static TextBlock CreatePrimaryText(string text, double fontSize, bool semiBold) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable Display"),
        FontSize = fontSize,
        FontWeight = semiBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = ResolveBrush("TextPrimaryBrush", "#F2F8FF"),
        TextWrapping = TextWrapping.WrapWholeWords,
    };

    private static TextBlock CreateSecondaryText(string text, double fontSize = 14) => new()
    {
        Text = text,
        FontSize = fontSize,
        Foreground = ResolveBrush("TextSecondaryBrush", "#B7CCE1"),
        TextWrapping = TextWrapping.WrapWholeWords,
    };

    private static TextBlock CreateMutedText(string text, double fontSize = 12) => new()
    {
        Text = text,
        FontSize = fontSize,
        Foreground = ResolveBrush("TextMutedBrush", "#7D9AB6"),
        TextWrapping = TextWrapping.WrapWholeWords,
    };

    private static TextBlock CreateAccentText(string text, double fontSize, int characterSpacing) => new()
    {
        Text = text,
        FontSize = fontSize,
        CharacterSpacing = characterSpacing,
        Foreground = ResolveBrush("AccentBrush", "#66E7FF"),
    };

    private static TextBlock CreateFieldLabel(string text)
    {
        var label = CreateMutedText(text, 12);
        label.CharacterSpacing = 60;
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
}
