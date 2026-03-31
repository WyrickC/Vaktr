using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Interfaces;
using Vaktr.Core.Models;

namespace Vaktr.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CollectorService _collectorService;
    private readonly IMetricStore _metricStore;
    private readonly IConfigStore _configStore;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly TrayIconHost _trayIcon;

    private bool _allowClose;
    private bool _hasShownTrayHint;

    public MainWindow(
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

        DataContext = _viewModel;

        _trayIcon = new TrayIconHost();
        _trayIcon.OpenRequested += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        _trayIcon.SettingsRequested += (_, _) => Dispatcher.Invoke(() =>
        {
            RestoreFromTray();
            ToggleSettings(true);
        });
        _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(() =>
        {
            _allowClose = true;
            Close();
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _collectorService.SnapshotCollected += OnSnapshotCollected;

        var config = _viewModel.BuildConfig();
        await _metricStore.InitializeAsync(config, CancellationToken.None);
        var history = await _metricStore.LoadHistoryAsync(DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);
        _viewModel.LoadHistory(history);
        _autoLaunchService.SetEnabled(config.LaunchOnStartup);
        await _collectorService.StartAsync(config, CancellationToken.None);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _collectorService.SnapshotCollected -= OnSnapshotCollected;
        _trayIcon.Dispose();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_viewModel.MinimizeToTray)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        DragMove();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ToggleSettings(!_viewModel.IsSettingsOpen);
    }

    private async void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyPanelVisibility();
        var config = _viewModel.BuildConfig();
        App.CurrentApp.ApplyTheme(config.Theme);
        _autoLaunchService.SetEnabled(config.LaunchOnStartup);
        await _configStore.SaveAsync(config, CancellationToken.None);
        await _collectorService.StartAsync(config, CancellationToken.None);
        ToggleSettings(false);
    }

    private void OnThemeQuickToggle(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedTheme = _viewModel.SelectedTheme == Vaktr.Core.Models.ThemeMode.Dark
            ? Vaktr.Core.Models.ThemeMode.Light
            : Vaktr.Core.Models.ThemeMode.Dark;
        App.CurrentApp.ApplyTheme(_viewModel.SelectedTheme);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MinimizeToTray)
        {
            HideToTray();
            return;
        }

        _allowClose = true;
        Close();
    }

    private void OnExpandedCloseClick(object sender, RoutedEventArgs e)
    {
        CloseExpandedPanel();
    }

    private void OnPanelExpandRequested(object? sender, Vaktr.App.Controls.PanelExpandRequestedEventArgs e)
    {
        _viewModel.ExpandedPanel = e.Panel;
        ExpandedOverlay.Visibility = Visibility.Visible;
        ExpandedOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }

    private async void OnSnapshotCollected(object? sender, MetricSnapshot snapshot)
    {
        await Dispatcher.InvokeAsync(() => _viewModel.ApplySnapshot(snapshot));
    }

    private void ToggleSettings(bool isOpen)
    {
        _viewModel.IsSettingsOpen = isOpen;
        var target = isOpen ? 0 : 396;
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        SettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void HideToTray()
    {
        Hide();
        if (_hasShownTrayHint)
        {
            return;
        }

        _trayIcon.ShowInfo("Vaktr is still running", "Use the tray icon to reopen the dashboard.");
        _hasShownTrayHint = true;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void CloseExpandedPanel()
    {
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        animation.Completed += (_, _) =>
        {
            ExpandedOverlay.Visibility = Visibility.Collapsed;
            _viewModel.ExpandedPanel = null;
        };

        ExpandedOverlay.BeginAnimation(OpacityProperty, animation);
    }
}
