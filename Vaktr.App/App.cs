using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        StartupTrace.Write("Resources initialized // launch-cut-v19");
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
        StartupTrace.Write("Stores and view model created // launch-cut-v19");
        ApplyThemeResources(config.Theme);
        StartupTrace.Write("Configured theme applied");

        _window = new ShellWindow(
            viewModel,
            metricStore,
            configStore,
            new AutoLaunchService());
        StartupTrace.Write("ShellWindow created // polished-v19");

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
                "#EEF5FB",
                "#F8FBFF",
                "#D1DEEC",
                "#FFFFFF",
                "#F4F8FC",
                "#EDF4FB",
                "#C3D4E6",
                "#EAF2FB",
                "#D7E5F2",
                "#0F2030",
                "#4A6078",
                "#6F8399",
                "#0E84B9",
                "#0C6C98",
                "#D9F1FF",
                "#1C7EA229",
                "#19D2A04A",
                "#8EE7EFF5")
            : new ThemePalette(
                "#030812",
                "#07101B",
                "#28445E",
                "#0C1726",
                "#112033",
                "#18314A",
                "#315274",
                "#091321",
                "#426A8C",
                "#F4F9FF",
                "#C6D7EA",
                "#8098B2",
                "#6FE2FF",
                "#C7F5FF",
                "#163B60",
                "#2B8FE6C4",
                "#1EFFA25C",
                "#B0060A12");

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
        StartupTrace.Write("EnsureResourcesInitialized start");
        Resources ??= new ResourceDictionary();
        EnsureFrameworkFallbackResources();
        StartupTrace.Write("Skipping XamlControlsResources merge for custom shell");
        StartupTrace.Write("EnsureResourcesInitialized complete");
    }

    private void EnsureFrameworkFallbackResources()
    {
        SetBrushFallback("AcrylicBackgroundFillColorDefaultBrush", "#15283B");
        SetBrushFallback("AcrylicInAppFillColorDefaultBrush", "#183148");
        SetBrushFallback("LayerFillColorDefaultBrush", "#102131");
        SetBrushFallback("LayerFillColorAltBrush", "#15283B");
        SetBrushFallback("CardStrokeColorDefaultBrush", "#27425E");
        StartupTrace.Write("Framework fallback resources seeded");
    }

    private void SetBrushFallback(string key, string hex)
    {
        if (!Resources.TryGetValue(key, out _))
        {
            Resources[key] = BrushFactory.CreateBrush(hex);
        }

        EnsureThemeBrushFallback("Default", key, hex);
        EnsureThemeBrushFallback("Dark", key, hex);
        EnsureThemeBrushFallback("Light", key, hex);
    }

    private void EnsureThemeBrushFallback(string themeKey, string resourceKey, string hex)
    {
        if (Resources.ThemeDictionaries.TryGetValue(themeKey, out var existing) &&
            existing is ResourceDictionary existingDictionary)
        {
            if (!existingDictionary.TryGetValue(resourceKey, out _))
            {
                existingDictionary[resourceKey] = BrushFactory.CreateBrush(hex);
            }

            return;
        }

        var dictionary = new ResourceDictionary
        {
            [resourceKey] = BrushFactory.CreateBrush(hex),
        };
        Resources.ThemeDictionaries[themeKey] = dictionary;
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
