using Microsoft.UI.Windowing;
using Microsoft.UI;
using System.Collections.Specialized;
using Vaktr.App.Controls;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;
using Windows.Graphics;

namespace Vaktr.App;

public sealed partial class ShellWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CollectorService _collectorService;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly TrayIconHost _trayIcon;

    private bool _allowClose;
    private bool _hasShownTrayHint;
    private bool _initialized;

    public ShellWindow(
        MainViewModel viewModel,
        CollectorService collectorService,
        IMetricStore metricStore,
        IConfigStore configStore,
        AutoLaunchService autoLaunchService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _collectorService = collectorService;
        _metricStore = metricStore;
        _configStore = configStore;
        _autoLaunchService = autoLaunchService;

        SettingsSplitView.DataContext = _viewModel;
        Title = "Vaktr";

        _trayIcon = new TrayIconHost(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _trayIcon.OpenRequested += (_, _) => DispatcherQueue.TryEnqueue(RestoreFromTray);
        _trayIcon.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            RestoreFromTray();
            ToggleSettings(true);
        });
        _trayIcon.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            _allowClose = true;
            Close();
        });

        _viewModel.DashboardPanels.CollectionChanged += OnDashboardPanelsChanged;
        SubscribeToPanels(_viewModel.DashboardPanels);

        Closed += OnWindowClosed;
        AppWindow.Resize(new SizeInt32(1480, 920));
        AppWindow.Closing += OnAppWindowClosing;
        ConfigureTitleBar();
    }

    public void ApplyTheme(ThemeMode mode)
    {
        SettingsSplitView.RequestedTheme = mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light;
        ConfigureTitleBar();
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _collectorService.SnapshotCollected += OnSnapshotCollected;

        var config = _viewModel.BuildConfig();
        await _metricStore.InitializeAsync(config, CancellationToken.None);
        var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);
        _viewModel.LoadHistory(history);
        ApplyTheme(config.Theme);
        _autoLaunchService.SetEnabled(config.LaunchOnStartup);
        await _collectorService.StartAsync(config, CancellationToken.None);
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _collectorService.SnapshotCollected -= OnSnapshotCollected;
        _viewModel.DashboardPanels.CollectionChanged -= OnDashboardPanelsChanged;
        UnsubscribeFromPanels(_viewModel.DashboardPanels);
        _trayIcon.Dispose();
        await _collectorService.DisposeAsync();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !_viewModel.MinimizeToTray)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ToggleSettings(!_viewModel.IsSettingsOpen);
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyPanelVisibility();
        var config = _viewModel.BuildConfig();
        ApplyTheme(config.Theme);
        _autoLaunchService.SetEnabled(config.LaunchOnStartup);
        await _configStore.SaveAsync(config, CancellationToken.None);
        await _collectorService.StartAsync(config, CancellationToken.None);
        ToggleSettings(false);
    }

    private void OnThemeQuickToggle(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedTheme = _viewModel.SelectedTheme == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;

        ApplyTheme(_viewModel.SelectedTheme);
    }

    private void OnExpandedCloseClick(object sender, RoutedEventArgs e)
    {
        CloseExpandedPanel();
    }

    private void OnPanelExpandRequested(object? sender, EventArgs e)
    {
        if (sender is not MetricPanelViewModel panel)
        {
            return;
        }

        _viewModel.ExpandedPanel = panel;
        ExpandedOverlay.Visibility = Visibility.Visible;
    }

    private void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => _viewModel.ApplySnapshot(snapshot));
    }

    private void ToggleSettings(bool isOpen)
    {
        _viewModel.IsSettingsOpen = isOpen;
        SettingsSplitView.IsPaneOpen = isOpen;
    }

    private void HideToTray()
    {
        AppWindow.Hide();
        if (_hasShownTrayHint)
        {
            return;
        }

        _trayIcon.ShowInfo("Vaktr is still running", "Use the tray icon to reopen the dashboard.");
        _hasShownTrayHint = true;
    }

    private void RestoreFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void CloseExpandedPanel()
    {
        ExpandedOverlay.Visibility = Visibility.Collapsed;
        _viewModel.ExpandedPanel = null;
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
    }

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

    private void ConfigureTitleBar()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var isDark = SettingsSplitView.RequestedTheme != ElementTheme.Light;
        var foreground = isDark ? Colors.White : Colors.Black;

        AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ForegroundColor = foreground;
        AppWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.InactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = isDark
            ? ColorHelper.FromArgb(28, 255, 255, 255)
            : ColorHelper.FromArgb(28, 0, 0, 0);
        AppWindow.TitleBar.ButtonPressedBackgroundColor = isDark
            ? ColorHelper.FromArgb(52, 255, 255, 255)
            : ColorHelper.FromArgb(52, 0, 0, 0);
    }
}
