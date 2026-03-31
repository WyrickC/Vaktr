using System.Configuration;
using System.Windows;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Models;
using Vaktr.Store.Persistence;

namespace Vaktr.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private CollectorService? _collectorService;

    public static App CurrentApp => (App)Current;

    public MainViewModel? MainViewModel { get; private set; }

    public void ApplyTheme(Vaktr.Core.Models.ThemeMode mode)
    {
        var mergedDictionaries = Resources.MergedDictionaries;
        var paletteIndex = mergedDictionaries
            .Select((dictionary, index) => new { dictionary.Source, index })
            .FirstOrDefault(entry => entry.Source is not null && entry.Source.OriginalString.Contains("Palette.", StringComparison.OrdinalIgnoreCase))
            ?.index ?? -1;

        var source = new Uri(
            mode == Vaktr.Core.Models.ThemeMode.Dark ? "Themes/Palette.Dark.xaml" : "Themes/Palette.Light.xaml",
            UriKind.Relative);
        if (paletteIndex >= 0)
        {
            mergedDictionaries[paletteIndex] = new ResourceDictionary { Source = source };
        }
        else
        {
            mergedDictionaries.Add(new ResourceDictionary { Source = source });
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configStore = new JsonConfigStore();
        var config = await configStore.LoadAsync(CancellationToken.None);
        ApplyTheme(config.Theme);

        var metricStore = new SqliteMetricStore();
        var collector = new WindowsMetricCollector();
        _collectorService = new CollectorService(collector, metricStore);

        MainViewModel = new MainViewModel(config);
        var window = new MainWindow(
            MainViewModel,
            _collectorService,
            metricStore,
            configStore,
            new AutoLaunchService());

        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_collectorService is not null)
        {
            await _collectorService.DisposeAsync();
        }

        base.OnExit(e);
    }
}

