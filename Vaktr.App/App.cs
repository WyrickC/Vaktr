using Microsoft.UI.Xaml;
using Vaktr.App.Services;
using Vaktr.App.ViewModels;
using Vaktr.Core.Models;
using Vaktr.Store.Persistence;

namespace Vaktr.App;

public sealed class App : Application
{
    private ShellWindow? _window;
    private bool _startupGuardActive = true;

    public static App CurrentApp => (App)Current;

    public App()
    {
        StartupTrace.Write("App ctor");
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        StartupTrace.Write("App.OnLaunched start");
        EnsureResourcesInitialized();
        ApplyThemeResources(ThemeMode.Dark);
        StartupTrace.Write("Default theme applied");

        var configStore = new JsonConfigStore();
        VaktrConfig config;
        try
        {
            config = configStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            StartupTrace.Write("Config loaded");
        }
        catch (Exception ex)
        {
            StartupTrace.WriteException("Config load", ex);
            config = VaktrConfig.CreateDefault().Normalize();
        }

        var metricStore = new SqliteMetricStore();
        var viewModel = new MainViewModel(config);
        StartupTrace.Write("Stores and view model created // launch-cut-v10");
        ApplyThemeResources(config.Theme);
        StartupTrace.Write("Configured theme applied");

        _window = new ShellWindow(
            viewModel,
            metricStore,
            configStore,
            new AutoLaunchService());
        StartupTrace.Write("ShellWindow created // polished-v10");

        _window.ApplyTheme(config.Theme);
        StartupTrace.Write("Window theme applied");
        _window.Activate();
        StartupTrace.Write("Window activated");
    }

    public void ApplyTheme(ThemeMode mode)
    {
        ApplyThemeResources(mode);
        _window?.ApplyTheme(mode);
    }

    public void MarkStartupSettled()
    {
        _startupGuardActive = false;
        StartupTrace.Write("Startup guard disarmed");
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

    private void EnsureResourcesInitialized()
    {
        Resources ??= new ResourceDictionary();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (e.Exception is not null)
        {
            StartupTrace.WriteException("App.UnhandledException", e.Exception);

            if (_startupGuardActive && e.Exception is System.Runtime.InteropServices.COMException)
            {
                e.Handled = true;
                StartupTrace.Write("Startup COMException handled to preserve shell");
            }
        }
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
