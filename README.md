# Vaktr

![Vaktr logo](C:/Repos/Vaktr/Vaktr.App/Assets/vaktr.png)

Vaktr is a local-first Windows telemetry dashboard. It is built for people who want a polished, always-on view of what their PC is doing right now and over time, without sending machine data to a cloud service.

## Who It Is For

- Windows power users who want a cleaner read on system behavior
- gamers and creators watching performance while they work or play
- developers and tinkerers who want Grafana-style visibility on a single Windows machine

## What Vaktr Does

Vaktr collects machine-local telemetry, stores a rolling history in SQLite, and renders it in a desktop dashboard with:

- live time-series panels
- at-a-glance summaries and gauges
- board-wide and per-panel time ranges
- drag-reorderable panels
- a control deck for scrape interval, retention, theme, and storage settings

## How It Works

Vaktr is split into three main parts:

- `Vaktr.Collector`
  Reads Windows counters and hardware sensors, then produces telemetry snapshots
- `Vaktr.Store`
  Persists snapshots and app settings locally
- `Vaktr.App`
  The WinUI 3 desktop shell that renders the dashboard

The basic flow is:

1. the collector samples the machine
2. the snapshot is written to local storage
3. the app updates the live board and historical views

## What Time-Series Metrics Are

A time-series metric is a value captured repeatedly over time.

Examples:

- CPU usage sampled every 2 seconds
- disk read throughput over the last 30 minutes
- memory use over the last 24 hours

That matters because a single number only tells you what is happening now, while a time-series tells you:

- whether the system is spiking, flat, or trending
- when a change started
- whether a workload is recurring
- how current behavior compares to the recent past

Vaktr uses time-series panels so you can see both the current reading and the shape of the workload behind it.

## Running Vaktr

From PowerShell:

```powershell
cd C:\Repos\Vaktr
dotnet restore .\Vaktr.sln
dotnet run --project .\Vaktr.App\Vaktr.App.csproj -p:Platform=x64
```

From Visual Studio:

1. Open [Vaktr.sln](C:/Repos/Vaktr/Vaktr.sln)
2. Set [Vaktr.App.csproj](C:/Repos/Vaktr/Vaktr.App/Vaktr.App.csproj) as the startup project
3. Choose `Debug` and `x64`
4. Run with `F5` or `Ctrl+F5`

The desktop process is `Vaktr.exe`.

## Data and Settings

Vaktr stores everything locally on the machine:

- settings: `%LocalAppData%\Vaktr\vaktr-settings.json`
- metrics database: `%LocalAppData%\Vaktr\Data\vaktr-metrics.db`

`%LocalAppData%` is intentional. Vaktr tracks machine-local telemetry, so the data should stay on the local node instead of roaming with a Windows profile.

## Defaults

- scrape interval: `2s`
- default board range: `15m`
- retention: `24h`
- storage path: `%LocalAppData%\Vaktr\Data`
- theme: dark

## Retention

Retention controls how much historical data Vaktr keeps.

Examples:

- `30m`
- `6h`
- `7d`

Vaktr prunes older data so the local database stays bounded while preserving a useful recent history window.

## Hardware Sensors

Vaktr uses LibreHardwareMonitor for hardware sensors such as temperatures. LibreHardwareMonitor’s own documentation notes that some sensors require administrator privileges to access, so hardware visibility can depend on the CPU family, motherboard, sensor path, and how the app is launched.

Source:

- [LibreHardwareMonitor README](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

## Project Goals

- local-only telemetry
- fast startup and low overhead
- smooth, polished desktop UX
- strong at-a-glance summaries plus deeper historical detail
- sensible defaults without turning the app into a wall of settings
