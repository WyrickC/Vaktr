using Microsoft.UI.Xaml;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Models;
using Vaktr.Store.Persistence;

namespace Vaktr.App;

public partial class App : Application
{
    private ShellWindow? _window;

    public static App CurrentApp => (App)Current;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var configStore = new JsonConfigStore();
        var config = await configStore.LoadAsync(CancellationToken.None);
        var metricStore = new SqliteMetricStore();
        var collectorService = new CollectorService(new WindowsMetricCollector(), metricStore);
        var viewModel = new MainViewModel(config);

        _window = new ShellWindow(
            viewModel,
            collectorService,
            metricStore,
            configStore,
            new AutoLaunchService());

        _window.ApplyTheme(config.Theme);
        _window.Activate();
    }

    public void ApplyTheme(ThemeMode mode) => _window?.ApplyTheme(mode);
}
