using Microsoft.UI.Xaml;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Collector;
using Vaktr.Core.Models;
using Vaktr.Store.Persistence;

namespace Vaktr.App;

public sealed class App : Application
{
    private ShellWindow? _window;

    public static App CurrentApp => (App)Current;

    public App()
    {
        ApplyThemeResources(ThemeMode.Dark);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var configStore = new JsonConfigStore();
        var config = await configStore.LoadAsync(CancellationToken.None);
        var metricStore = new SqliteMetricStore();
        var collectorService = new CollectorService(new WindowsMetricCollector(), metricStore);
        var viewModel = new MainViewModel(config);
        ApplyThemeResources(config.Theme);

        _window = new ShellWindow(
            viewModel,
            collectorService,
            metricStore,
            configStore,
            new AutoLaunchService());

        _window.ApplyTheme(config.Theme);
        _window.Activate();
    }

    public void ApplyTheme(ThemeMode mode)
    {
        ApplyThemeResources(mode);
        _window?.ApplyTheme(mode);
    }

    private void ApplyThemeResources(ThemeMode mode)
    {
        var palette = mode == ThemeMode.Light
            ? new ThemePalette(
                "#EEF4FA",
                "#FBFDFF",
                "#D5E0EB",
                "#FFFFFF",
                "#F3F7FB",
                "#E6EEF6",
                "#C1D1E0",
                "#EAF3FB",
                "#DCE9F5",
                "#102236",
                "#42556A",
                "#6A7D92",
                "#0E7C98",
                "#0B6178",
                "#D9F6FF",
                "#1A8EB833",
                "#16D98A2E",
                "#8AE4EDF3")
            : new ThemePalette(
                "#061018",
                "#0B1622",
                "#1E3144",
                "#102131",
                "#15283B",
                "#183148",
                "#27425E",
                "#11283C",
                "#22405C",
                "#F2F8FF",
                "#B7CCE1",
                "#7D9AB6",
                "#66E7FF",
                "#B7F7FF",
                "#10394D",
                "#1B68DAFF",
                "#15FF9B54",
                "#A0060C14");

        SetBrushResource("AppBackdropBrush", palette.AppBackdrop);
        SetBrushResource("ShellBackgroundBrush", palette.ShellBackground);
        SetBrushResource("ShellStrokeBrush", palette.ShellStroke);
        SetBrushResource("SurfaceBrush", palette.Surface);
        SetBrushResource("SurfaceElevatedBrush", palette.SurfaceElevated);
        SetBrushResource("SurfaceStrongBrush", palette.SurfaceStrong);
        SetBrushResource("SurfaceStrokeBrush", palette.SurfaceStroke);
        SetBrushResource("PanelOverlayBrush", palette.PanelOverlay);
        SetBrushResource("SurfaceGridBrush", palette.SurfaceGrid);
        SetBrushResource("TextPrimaryBrush", palette.TextPrimary);
        SetBrushResource("TextSecondaryBrush", palette.TextSecondary);
        SetBrushResource("TextMutedBrush", palette.TextMuted);
        SetBrushResource("AccentBrush", palette.Accent);
        SetBrushResource("AccentStrongBrush", palette.AccentStrong);
        SetBrushResource("AccentSoftBrush", palette.AccentSoft);
        SetBrushResource("AccentHaloBrush", palette.AccentHalo);
        SetBrushResource("WarningHaloBrush", palette.WarningHalo);
        SetBrushResource("OverlayScrimBrush", palette.OverlayScrim);
    }

    private void SetBrushResource(string key, string hex)
    {
        if (Resources.TryGetValue(key, out var existing) && existing is SolidColorBrush brush)
        {
            brush.Color = BrushFactory.ParseColor(hex);
            return;
        }

        Resources[key] = BrushFactory.CreateBrush(hex);
    }

    private sealed record ThemePalette(
        string AppBackdrop,
        string ShellBackground,
        string ShellStroke,
        string Surface,
        string SurfaceElevated,
        string SurfaceStrong,
        string SurfaceStroke,
        string PanelOverlay,
        string SurfaceGrid,
        string TextPrimary,
        string TextSecondary,
        string TextMuted,
        string Accent,
        string AccentStrong,
        string AccentSoft,
        string AccentHalo,
        string WarningHalo,
        string OverlayScrim);
}
