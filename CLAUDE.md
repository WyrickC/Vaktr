# CLAUDE.md - Vaktr

## Project Overview

Vaktr is a real-time, local-first Windows system monitoring dashboard. It provides live telemetry and historical data visualization with zero cloud connectivity, zero configuration, and full offline operation. Target users: gamers, creators, developers, power users, IT professionals.

**Current version:** v1.0.1 (in development on `cwyrick/v1.0.1` branch)

## Tech Stack

- **Language:** C# (.NET 8, C# 11+)
- **UI:** WinUI 3 (Windows App SDK 1.8)
- **Database:** SQLite (WAL mode) via Microsoft.Data.Sqlite
- **Sensors:** LibreHardwareMonitorLib 0.9.6, PDH (P/Invoke), WMI
- **Testing:** xUnit 2.7.0
- **Installer:** Inno Setup 6
- **CI/CD:** GitHub Actions
- **Min OS:** Windows 10 build 17763 (1809)

## Solution Structure

```
Vaktr.sln
├── Vaktr.Core/          # Domain models, interfaces, enums (no external deps)
├── Vaktr.Collector/     # Windows metric collection (PDH, WMI, LibreHardwareMonitor)
├── Vaktr.Store/         # Persistence (SQLite metrics, JSON config)
├── Vaktr.App/           # WinUI 3 desktop application (entry point)
├── Vaktr.Tests/         # xUnit test suite
├── installer/           # Inno Setup script
└── tools/               # SensorProbe debugging utility
```

**Dependency graph:** `App → Core, Collector, Store` | `Collector → Core` | `Store → Core` | `Tests → Core, Collector, Store`

## Build & Run

```bash
# Restore and build
dotnet restore Vaktr.sln
dotnet build Vaktr.sln -c Release -p:Platform=x64

# Run
dotnet run --project Vaktr.App/Vaktr.App.csproj -p:Platform=x64

# Test
dotnet test Vaktr.Tests/Vaktr.Tests.csproj -c Release

# Publish
dotnet publish Vaktr.App/Vaktr.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish/x64

# Installer (requires Inno Setup 6)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion="1.0.1" installer/vaktr-setup.iss
```

## Architecture

**Layered architecture with event-driven data flow:**

```
WindowsMetricCollector (PDH/WMI/LibreHW samples)
  → CollectorService (PeriodicTimer tick)
  → SqliteMetricStore (append + retention prune)
  → SnapshotCollected event
  → ShellWindow (DispatcherQueue UI update)
  → TelemetryChart.Series (canvas redraw)
```

- **Core:** Records for immutable data (MetricSnapshot, MetricSample, MetricPoint). VaktrConfig with normalization/validation.
- **Collector:** PDH counters with WMI fallback. SemaphoreSlim gates for thread-safe state. CancellationToken propagation. 3s initial / 5s recurring collection timeouts.
- **Store:** SQLite with `metric_samples` (raw) + `metric_rollups_1m` (aggregated) tables. PRAGMA-optimized. Auto retention pruning.
- **App:** MVVM via ObservableObject. Custom canvas controls (TelemetryChart, UsageGauge). Dark/Light theme palettes. Single-instance via COM wrapper.

## Key Files

| Purpose | File |
|---------|------|
| Entry point | `Vaktr.App/Program.cs` |
| App lifecycle | `Vaktr.App/App.cs` |
| Main window | `Vaktr.App/ShellWindow.Polished.cs` |
| ViewModel | `Vaktr.App/ViewModels/DashboardViewModels.cs` |
| Chart control | `Vaktr.App/Controls/TelemetryChart.cs` |
| Metric collector | `Vaktr.Collector/WindowsMetricCollector.cs` |
| Collection service | `Vaktr.Collector/CollectorService.cs` |
| PDH interop | `Vaktr.Collector/Interop/PdhNative.cs` |
| SQLite store | `Vaktr.Store/Persistence/SqliteMetricStore.cs` |
| Config store | `Vaktr.Store/Persistence/JsonConfigStore.cs` |
| Domain models | `Vaktr.Core/Models/Metrics.cs` |
| Config model | `Vaktr.Core/Models/VaktrConfig.cs` |
| Interfaces | `Vaktr.Core/Interfaces/Contracts.cs` |
| Enums | `Vaktr.Core/Models/Enums.cs` |
| Installer | `installer/vaktr-setup.iss` |

## Code Conventions

- File-scoped namespaces (`namespace X;`)
- Implicit usings + nullable reference types enabled
- PascalCase public members, camelCase locals, `_prefixed` private fields
- Async/await throughout (async-first)
- Sealed classes where inheritance isn't needed
- Records for immutable data
- PeriodicTimer over Task.Delay loops
- DispatcherQueue for UI thread marshalling
- Graceful degradation (PDH → WMI fallback)

## Data Paths

- **Settings:** `%LocalAppData%\Vaktr\vaktr-settings.json`
- **Database:** `%LocalAppData%\Vaktr\Data\vaktr-metrics.db`
- **Legacy (migration):** `%AppData%\Vaktr\`

## CI/CD

- **ci.yml:** PR builds + tests on `windows-latest`
- **release.yml:** Tag-triggered (v*) or manual. Publishes + builds installer + creates GitHub Release
- **security.yml:** Weekly dependency audit, Gitleaks secrets scan, code quality checks

## Secondary Repo

`Vaktr.Web` (at `c:\Repos\Vaktr.Web`) is the marketing/docs website - pure HTML/CSS/JS, no build tools.
