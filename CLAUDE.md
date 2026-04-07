# Vaktr

Local-first Windows telemetry dashboard. Grafana-style time-series panels for a single PC, no cloud dependency. Built with WinUI 3 on .NET 8.

## Architecture

```
Vaktr.Core        – Models, enums, interfaces (netstandard-compatible)
Vaktr.Collector   – PDH counters, WMI, LibreHardwareMonitor, process enumeration
Vaktr.Store       – SQLite persistence (WAL mode) + JSON config store
Vaktr.App         – WinUI 3 desktop shell (single-window dashboard)
Vaktr.Tests       – Unit tests (xUnit)
tools/SensorProbe – Standalone sensor discovery utility
```

### Data flow

1. `CollectorService` runs a `PeriodicTimer` (default 2s)
2. `WindowsMetricCollector.CollectAsync()` produces a `MetricSnapshot`
3. `SqliteMetricStore.AppendSnapshotAsync()` persists it (transactional)
4. `SnapshotCollected` event fires → `MainViewModel.ApplySnapshot()` updates the UI via `DispatcherQueue`

### Key types

| Type | Location | Role |
|------|----------|------|
| `MetricSnapshot` | Core/Models/Metrics.cs | One collection cycle's worth of samples |
| `MetricSample` | Core/Models/Metrics.cs | Single metric reading (panel + series + value) |
| `VaktrConfig` | Core/Models/VaktrConfig.cs | All user-facing settings |
| `WindowsMetricCollector` | Collector/ | PDH + WMI + LibreHardwareMonitor |
| `CollectorService` | Collector/ | Timer-driven collection loop |
| `SqliteMetricStore` | Store/Persistence/ | Append, load history, prune/rollup |
| `JsonConfigStore` | Store/Persistence/ | Settings persistence (%LOCALAPPDATA%\Vaktr) |
| `MainViewModel` | App/ViewModels/ | Dashboard state, panel management |
| `MetricPanelViewModel` | App/ViewModels/DashboardViewModels.cs | Per-panel series buffers and display state |
| `ShellWindow` | App/ | Main window (split across partial classes) |

### Storage

- Settings: `%LOCALAPPDATA%\Vaktr\vaktr-settings.json`
- Metrics DB: `%LOCALAPPDATA%\Vaktr\Data\vaktr-metrics.db`
- Raw samples kept 6 hours, then rolled up to 1-minute aggregates
- Background pruning every 15 minutes

## Build & run

```powershell
dotnet restore Vaktr.sln
dotnet run --project Vaktr.App\Vaktr.App.csproj -p:Platform=x64
```

Or open `Vaktr.sln` in Visual Studio, set `Vaktr.App` as startup, `Debug|x64`, F5.

Target: .NET 8, Windows 10 1809+ (10.0.17763.0), WindowsAppSDK 1.8.

## Tests

```powershell
dotnet test Vaktr.Tests\Vaktr.Tests.csproj
```

## Conventions

- All data models are **sealed records** in `Vaktr.Core`
- UI is mostly **procedural C#** (ShellWindow.Polished.UI.cs builds the tree in code), not XAML
- ShellWindow is split into partial classes: `ShellWindow.cs` (lifecycle), `ShellWindow.Polished.cs` (logic), `ShellWindow.Polished.UI.cs` (layout), `ShellWindow.Minimal.cs` (fallback)
- Thread safety via `SemaphoreSlim` in store and collector service
- SQLite uses WAL mode, prepared statements, transaction batching
- Temperature collection has graceful fallbacks (LibreHardwareMonitor → WMI → skip)
- Collector runs on `BelowNormal` thread priority to minimize overhead

## Defaults

- Scrape interval: 2s
- Board time range: 15m
- Retention: 24h
- Theme: dark
