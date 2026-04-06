# Vaktr

Vaktr is a local-first Windows telemetry dashboard. It gives you a polished, always-on view of what your PC is doing right now and over time, without sending any data to a cloud service. Think Grafana-style time-series panels, but for a single Windows machine.

## Who It Is For

- Windows power users who want a cleaner read on system behavior
- Gamers and creators watching performance while they work or play
- Developers and tinkerers who want Grafana-style visibility on a single Windows machine

## What Vaktr Shows

- **CPU** - total usage, per-core breakdown, clock frequency, temperature
- **GPU** - usage, dedicated memory, temperature
- **Memory** - used/available with percentage
- **Disk** - read/write throughput per logical drive, drive capacity gauges
- **Network** - download/upload per interface
- **System** - process count, thread count, handle count
- **Process table** - per-process CPU and memory breakdown (on CPU and Memory panels)

Each metric is rendered as a live time-series chart. Panels can be expanded for a focused view, reordered by dragging, and zoomed into any time window.

## Architecture

```
Vaktr.Core        Data models, enums, interfaces (no UI dependency)
Vaktr.Collector   PDH counters, WMI, LibreHardwareMonitor, process enumeration
Vaktr.Store       SQLite persistence (WAL mode) + JSON config store
Vaktr.App         WinUI 3 desktop shell (single-window dashboard)
Vaktr.Tests       Unit tests
tools/SensorProbe Standalone sensor discovery utility
```

### Data flow

1. `CollectorService` drives a `PeriodicTimer` (default 2 s)
2. `WindowsMetricCollector` samples PDH counters, memory, drives, network, temperatures, and processes
3. `SqliteMetricStore` persists the snapshot in a transaction
4. The `SnapshotCollected` event fires and `MainViewModel` updates the UI via `DispatcherQueue`

### Storage

- **Settings:** `%LOCALAPPDATA%\Vaktr\vaktr-settings.json`
- **Metrics DB:** `%LOCALAPPDATA%\Vaktr\Data\vaktr-metrics.db`
- Raw samples are kept for up to 6 hours, then rolled up into 1-minute aggregates
- Background pruning runs every 15 minutes and honors the configured retention window

## Running Vaktr

### From a terminal

```powershell
cd C:\Repos\Vaktr
dotnet restore Vaktr.sln
dotnet build Vaktr.sln -p:Platform=x64
dotnet run --project Vaktr.App/Vaktr.App.csproj -p:Platform=x64
```

> Use forward slashes for `dotnet run` in Git Bash. PowerShell handles both.

### From Visual Studio

1. Open `Vaktr.sln`
2. Set `Vaktr.App` as the startup project
3. Choose **Debug | x64**
4. Press **F5**

The desktop process is `Vaktr.exe`.

## Requirements

- .NET 8 SDK
- Windows 10 1809+ (build 17763)
- Windows App SDK 1.8
- Platform target: **x64**, x86, or ARM64 (`AnyCPU` is not supported for the App project)

## Defaults

| Setting | Default |
|---------|---------|
| Scrape interval | 2 s |
| Graph window | 15 min |
| Retention | 24 h |
| Storage path | `%LOCALAPPDATA%\Vaktr\Data` |
| Theme | Dark |

## Retention

Retention controls how much historical data Vaktr keeps. You can set it with a number and unit:

- `30m` - 30 minutes
- `6h` - 6 hours
- `7d` - 7 days

Vaktr prunes data older than the retention window on a rolling basis so the local database stays bounded.

## Hardware Sensors

Vaktr uses [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for hardware sensors. GPU temperatures (AMD and Nvidia) work out of the box. CPU temperature monitoring is on the [roadmap](ROADMAP.md) — it requires a signed kernel driver that will be bundled into a future Vaktr installer.

## Tests

```powershell
dotnet test Vaktr.Tests/Vaktr.Tests.csproj
```

## Project Goals

- Local-only telemetry, no cloud dependency
- Fast startup and low overhead
- Smooth, polished desktop UX
- Strong at-a-glance summaries plus deeper historical detail
- Sensible defaults without a wall of settings
