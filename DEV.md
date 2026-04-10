# DEV.md - Vaktr Developer Reference

Complete technical reference for every folder, file, function, class, and tool in the Vaktr codebase.

---

## Table of Contents

1. [What Is Vaktr](#1-what-is-vaktr)
2. [Tech Stack](#2-tech-stack)
3. [Repository Layout](#3-repository-layout)
4. [Solution & Build Configuration](#4-solution--build-configuration)
5. [Vaktr.Core - Domain Models & Contracts](#5-vaktrcore---domain-models--contracts)
6. [Vaktr.Collector - Telemetry Collection](#6-vaktrcollector---telemetry-collection)
7. [Vaktr.Store - Persistence Layer](#7-vaktrstore---persistence-layer)
8. [Vaktr.App - WinUI 3 Desktop Application](#8-vaktrapp---winui-3-desktop-application)
9. [Vaktr.Tests - Test Suite](#9-vaktrtests---test-suite)
10. [Tools - SensorProbe](#10-tools---sensorprobe)
11. [Installer - Inno Setup](#11-installer---inno-setup)
12. [CI/CD - GitHub Actions](#12-cicd---github-actions)
13. [Data Flow & Runtime Architecture](#13-data-flow--runtime-architecture)
14. [Theme System](#14-theme-system)
15. [Data Paths & Storage](#15-data-paths--storage)

---

## 1. What Is Vaktr

Vaktr is a real-time, local-first Windows system monitoring dashboard. It collects CPU, GPU, memory, disk, network, and temperature telemetry, then renders it as live charts with historical data persistence. The entire application runs offline with zero cloud connectivity, zero configuration, and zero telemetry.

**Target audience:** gamers, creators, developers, power users, and IT professionals.

**Core principles:**
- Zero setup - install and run
- Beautiful real-time charts with smooth rendering
- Historical data persistence in SQLite
- Completely offline - no accounts, no cloud, no telemetry
- Self-contained binary - no runtime dependencies for the end user

---

## 2. Tech Stack

### Languages & Frameworks

| Technology | Version | Purpose |
|---|---|---|
| **C#** | 11+ | Primary language (records, file-scoped namespaces, nullable refs) |
| **.NET** | 8.0 | Runtime and SDK |
| **WinUI 3** | Windows App SDK 1.8.260317003 | Desktop UI framework |
| **SQLite** | via Microsoft.Data.Sqlite 8.0.0 | Local database for metric persistence |

### Libraries

| Package | Version | Purpose |
|---|---|---|
| **Microsoft.WindowsAppSDK** | 1.8.260317003 | WinUI 3 framework and window management |
| **LibreHardwareMonitorLib** | 0.9.6 | GPU sensors, CPU/GPU temperature monitoring |
| **System.Management** | 10.0.2 | WMI queries for system info and fallback metrics |
| **Microsoft.Data.Sqlite** | 8.0.0 | SQLite database access with ADO.NET |

### Testing

| Package | Version | Purpose |
|---|---|---|
| **xUnit** | 2.7.0 | Unit testing framework |
| **xunit.runner.visualstudio** | 2.5.7 | Visual Studio test runner integration |
| **Microsoft.NET.Test.Sdk** | 17.9.0 | Test SDK host |
| **coverlet.collector** | 6.0.1 | Code coverage collection |

### Build & Distribution

| Tool | Purpose |
|---|---|
| **dotnet CLI / MSBuild** | Build, restore, publish, test |
| **Inno Setup 6** | Windows installer compiler (LZMA2/ultra64 compression) |
| **GitHub Actions** | CI/CD pipelines (build, test, release, security scans) |
| **Gitleaks** | Secrets scanning in CI |

### Platform Requirements

| Requirement | Value |
|---|---|
| **Target Framework** | net8.0-windows10.0.19041.0 |
| **Minimum OS** | Windows 10 build 17763 (version 1809) |
| **Architectures** | x64 (primary), ARM64, x86 |
| **DPI Awareness** | PerMonitorV2 |
| **Execution Level** | asInvoker (no elevation required) |
| **Runtime** | Self-contained (bundled .NET runtime) |

### Native APIs (P/Invoke)

| API | DLL | Purpose |
|---|---|---|
| **PDH** (Performance Data Helper) | pdh.dll | High-performance counter queries for CPU, disk, network, GPU |
| **GlobalMemoryStatusEx** | kernel32.dll | Physical/virtual memory statistics |
| **GetSystemTimes** | kernel32.dll | CPU idle/kernel/user time (fallback) |
| **CreateToolhelp32Snapshot** | kernel32.dll | Process enumeration |
| **OpenProcess / GetProcessTimes** | kernel32.dll | Per-process CPU measurement |
| **GetProcessMemoryInfo** | kernel32.dll | Per-process memory usage |
| **GetProcessHandleCount** | kernel32.dll | Per-process handle count |
| **LoadImageW / SendMessageW** | user32.dll | Window icon loading |

---

## 3. Repository Layout

```
Vaktr/
├── Vaktr.sln                          # Visual Studio solution (5 projects)
├── Directory.Build.props              # Shared MSBuild properties (empty, reserved)
├── Directory.Build.targets            # Shared MSBuild targets (empty, reserved)
├── NuGet.Config                       # NuGet feed configuration (default)
├── LICENSE                            # Project license
├── README.md                          # User-facing documentation
├── ROADMAP.md                         # Planned features
├── CLAUDE.md                          # AI assistant context file
│
├── Vaktr.Core/                        # Layer 1: Domain models & contracts
│   ├── Vaktr.Core.csproj
│   ├── Interfaces/
│   │   └── Contracts.cs               # IMetricCollector, IMetricStore, IConfigStore
│   └── Models/
│       ├── Enums.cs                   # MetricCategory, MetricUnit, ThemeMode, presets
│       ├── Metrics.cs                 # MetricSnapshot, MetricSample, MetricPoint, etc.
│       └── VaktrConfig.cs             # Configuration model with normalization/validation
│
├── Vaktr.Collector/                   # Layer 2: Windows telemetry collection
│   ├── Vaktr.Collector.csproj
│   ├── WindowsMetricCollector.cs      # Main metric sampling (PDH, WMI, LibreHW)
│   ├── CollectorService.cs            # Collection lifecycle (start/stop/timer)
│   ├── TemperatureSensorReader.cs     # CPU/GPU temperature via LibreHardwareMonitor
│   └── Interop/
│       ├── PdhNative.cs               # P/Invoke for pdh.dll (perf counters)
│       └── ProcessNative.cs           # P/Invoke for kernel32.dll (process enum)
│
├── Vaktr.Store/                       # Layer 3: Data persistence
│   ├── Vaktr.Store.csproj
│   └── Persistence/
│       ├── SqliteMetricStore.cs       # SQLite storage (raw + rollup tables)
│       └── JsonConfigStore.cs         # JSON settings serialization
│
├── Vaktr.App/                         # Layer 4: WinUI 3 desktop application
│   ├── Vaktr.App.csproj
│   ├── app.manifest                   # UAC, DPI awareness, OS compatibility
│   ├── WinUiGlobalUsings.cs           # Global using directives
│   ├── Program.cs                     # Entry point (COM, dispatcher, exceptions)
│   ├── App.cs                         # Application lifecycle & theme management
│   ├── ShellWindow.Polished.cs        # Main window logic & event handlers
│   ├── ShellWindow.Polished.Ui.cs     # Main window UI construction methods
│   ├── ViewModels/
│   │   ├── ObservableObject.cs        # INotifyPropertyChanged base class
│   │   └── DashboardViewModels.cs     # MainViewModel + panel/summary VMs
│   ├── Controls/
│   │   ├── TelemetryChart.cs          # Canvas-based line chart with zoom
│   │   ├── TelemetryPanelCard.cs      # Complete metric panel card
│   │   ├── UsageGauge.cs              # Radial arc gauge control
│   │   ├── ActionChip.cs              # Interactive button/chip control
│   │   ├── InlineTextEntry.cs         # Custom text input control
│   │   └── IconFactory.cs             # Icon/tile generation utility
│   ├── Services/
│   │   ├── AutoLaunchService.cs       # Windows registry startup management
│   │   └── StartupTrace.cs            # Diagnostic startup logging
│   └── Assets/
│       ├── Vaktr.ico                  # Application icon
│       └── vaktr.png                  # Brand image
│
├── Vaktr.Tests/                       # Unit tests
│   ├── Vaktr.Tests.csproj
│   ├── Usings.cs                      # Global test usings
│   └── UnitTest1.cs                   # VaktrConfigTests
│
├── tools/
│   └── SensorProbe/                   # Hardware sensor diagnostic utility
│       ├── SensorProbe.csproj
│       └── Program.cs
│
├── installer/
│   └── vaktr-setup.iss                # Inno Setup installer script
│
├── publish/                           # Build output directory
│   └── x64/                           # Self-contained x64 binaries
│
└── .github/
    └── workflows/
        ├── ci.yml                     # PR build + test pipeline
        ├── release.yml                # Tag-triggered release + installer
        └── security.yml               # Weekly dependency/secrets/quality scans
```

---

## 4. Solution & Build Configuration

### Vaktr.sln

Visual Studio 2022 solution containing 5 projects with platform configurations for x86, x64, and ARM64 in both Debug and Release modes.

**Project dependency graph:**
```
Vaktr.App ──> Vaktr.Core
          ──> Vaktr.Collector ──> Vaktr.Core
          ──> Vaktr.Store ──────> Vaktr.Core

Vaktr.Tests ──> Vaktr.Core
            ──> Vaktr.Collector
            ──> Vaktr.Store
```

### Project Targets

| Project | Target Framework | Output | Windows-Specific |
|---|---|---|---|
| Vaktr.Core | net8.0 | Library | No |
| Vaktr.Collector | net8.0-windows | Library | Yes |
| Vaktr.Store | net8.0 | Library | No |
| Vaktr.App | net8.0-windows10.0.19041.0 | WinExe | Yes |
| Vaktr.Tests | net8.0-windows | Library | Yes |

### Vaktr.App.csproj Key Settings

- **OutputType:** WinExe
- **AssemblyName:** Vaktr
- **ApplicationIcon:** Assets\Vaktr.ico
- **ApplicationManifest:** app.manifest
- **UseWinUI:** true
- **WindowsPackageType:** None (not packaged as MSIX/UWP)
- **ImplicitUsings:** disabled (explicit global usings in WinUiGlobalUsings.cs)
- **RuntimeIdentifiers:** win-x86, win-x64, win-arm64
- **Default Platform:** x64

### app.manifest

```xml
<requestedExecutionLevel level="asInvoker" uiAccess="false" />
<dpiAwareness>PerMonitorV2</dpiAwareness>
<supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />  <!-- Windows 10/11 -->
```

### Build Commands

```bash
# Restore
dotnet restore Vaktr.sln

# Build
dotnet build Vaktr.sln -c Release -p:Platform=x64

# Run
dotnet run --project Vaktr.App/Vaktr.App.csproj -p:Platform=x64

# Test
dotnet test Vaktr.Tests/Vaktr.Tests.csproj -c Release

# Publish (self-contained)
dotnet publish Vaktr.App/Vaktr.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish/x64
```

---

## 5. Vaktr.Core - Domain Models & Contracts

Pure domain layer with zero external dependencies. Defines the data structures and service contracts used by all other layers.

### Interfaces/Contracts.cs

#### `IMetricCollector` : IAsyncDisposable
Contract for collecting system metrics.

| Method | Signature | Description |
|---|---|---|
| `CollectAsync` | `Task<MetricSnapshot> CollectAsync(CancellationToken)` | Collects a single snapshot of all system metrics |

#### `IMetricStore` : IAsyncDisposable
Contract for persisting and querying metric data.

| Method | Signature | Description |
|---|---|---|
| `InitializeAsync` | `Task InitializeAsync(VaktrConfig, CancellationToken)` | Creates schema, opens connection |
| `AppendSnapshotAsync` | `Task AppendSnapshotAsync(MetricSnapshot, CancellationToken)` | Inserts a snapshot's samples into the database |
| `LoadHistoryAsync` | `Task<IReadOnlyList<MetricSeriesHistory>> LoadHistoryAsync(DateTimeOffset fromUtc, CancellationToken)` | Loads historical data from a given time |
| `PruneAsync` | `Task PruneAsync(VaktrConfig, CancellationToken)` | Removes data older than the retention window |

#### `IConfigStore`
Contract for loading and saving application configuration.

| Method | Signature | Description |
|---|---|---|
| `LoadAsync` | `Task<VaktrConfig> LoadAsync(CancellationToken)` | Loads config from disk (or returns default) |
| `SaveAsync` | `Task SaveAsync(VaktrConfig, CancellationToken)` | Persists config to disk |

---

### Models/Enums.cs

#### `MetricCategory` (enum)
Categorizes what a metric measures.

| Value | Int | Description |
|---|---|---|
| `Cpu` | 0 | CPU utilization, frequency, per-core |
| `Memory` | 1 | RAM usage |
| `Disk` | 2 | Disk I/O throughput and drive usage |
| `Network` | 3 | Network send/receive throughput |
| `System` | 4 | Process counts, threads, handles |
| `Gpu` | 5 | GPU utilization, VRAM, temperature |

#### `MetricUnit` (enum)
The unit of measurement for a metric value.

| Value | Int | Description |
|---|---|---|
| `Percent` | 0 | 0-100% utilization |
| `Gigabytes` | 1 | Memory/storage in GB |
| `MegabytesPerSecond` | 2 | Disk I/O throughput |
| `MegabitsPerSecond` | 3 | Network throughput |
| `Megahertz` | 4 | CPU/GPU clock frequency |
| `Count` | 5 | Absolute count (processes, threads) |
| `Celsius` | 6 | Temperature reading |

#### `ThemeMode` (enum)
| Value | Int |
|---|---|
| `Dark` | 0 |
| `Light` | 1 |

#### `TimeRangePreset` (enum)
Predefined time windows for chart display. The int value represents minutes.

| Value | Minutes | Display |
|---|---|---|
| `OneMinute` | 1 | 1m |
| `FiveMinutes` | 5 | 5m |
| `FifteenMinutes` | 15 | 15m |
| `ThirtyMinutes` | 30 | 30m |
| `OneHour` | 60 | 1h |
| `TwelveHours` | 720 | 12h |
| `TwentyFourHours` | 1440 | 24h |
| `SevenDays` | 10080 | 7d |
| `ThirtyDays` | 43200 | 30d |

#### `RetentionPreset` (enum)
Data retention period options. The int value represents days.

| Value | Days |
|---|---|
| `Unlimited` | 0 |
| `OneDay` | 1 |
| `SevenDays` | 7 |
| `ThirtyDays` | 30 |
| `NinetyDays` | 90 |

---

### Models/Metrics.cs

All data records are `sealed record` types (immutable, value-based equality).

#### `MetricPoint`
A single timestamped value.

| Property | Type | Description |
|---|---|---|
| `Timestamp` | `DateTimeOffset` | When the value was sampled |
| `Value` | `double` | The metric value |

#### `ProcessActivitySample`
A snapshot of one process's resource usage.

| Property | Type | Description |
|---|---|---|
| `ProcessId` | `int` | OS process ID |
| `Name` | `string` | Process executable name |
| `CpuPercent` | `double` | CPU usage percentage |
| `MemoryGigabytes` | `double` | Working set in GB |
| `ThreadCount` | `int` | Number of threads |
| `HandleCount` | `int` | Number of OS handles |

#### `LiveBoardDetails`
Enriched details attached to a snapshot for process-level data.

| Property | Type | Description |
|---|---|---|
| `Processes` | `IReadOnlyList<ProcessActivitySample>` | All sampled processes |

#### `MetricSample`
A single metric reading with full identification metadata.

| Property | Type | Description |
|---|---|---|
| `PanelKey` | `string` | Unique panel identifier (e.g., `"cpu-total"`) |
| `PanelTitle` | `string` | Display name (e.g., `"CPU Total"`) |
| `SeriesKey` | `string` | Series identifier within a panel |
| `SeriesName` | `string` | Display name for the series |
| `Category` | `MetricCategory` | Which category this belongs to |
| `Unit` | `MetricUnit` | Unit of measurement |
| `Value` | `double` | The measured value |
| `Timestamp` | `DateTimeOffset` | When it was measured |

#### `MetricSnapshot`
A complete collection of all metrics at a point in time.

| Property | Type | Description |
|---|---|---|
| `Timestamp` | `DateTimeOffset` | Collection timestamp |
| `Samples` | `IReadOnlyList<MetricSample>` | All metric samples |
| `LiveDetails` | `LiveBoardDetails?` | Optional process-level details (default: null) |

#### `MetricSeriesHistoryItem`
Historical data points for one series.

| Property | Type | Description |
|---|---|---|
| `SeriesKey` | `string` | Series identifier |
| `SeriesName` | `string` | Display name |
| `Points` | `IReadOnlyList<MetricPoint>` | Time-series data |

#### `MetricSeriesHistory`
Historical data for one panel (may contain multiple series).

| Property | Type | Description |
|---|---|---|
| `PanelKey` | `string` | Panel identifier |
| `PanelTitle` | `string` | Display name |
| `Category` | `MetricCategory` | Metric category |
| `Unit` | `MetricUnit` | Unit of measurement |
| `Series` | `IReadOnlyList<MetricSeriesHistoryItem>` | All series within this panel |

---

### Models/VaktrConfig.cs

Mutable configuration class with normalization, validation, and path resolution.

#### Constants (private)

| Constant | Value | Description |
|---|---|---|
| `DefaultScrapeIntervalSecondsValue` | 2 | Default collection interval |
| `DefaultGraphWindowMinutesValue` | 15 | Default chart window |
| `DefaultMaxRetentionHoursValue` | 24 | Default data retention |
| `MaxGraphWindowMinutesValue` | 43200 | Maximum chart window (30 days) |

#### Instance Properties (serialized to JSON)

| Property | Type | Default | Description |
|---|---|---|---|
| `ScrapeIntervalSeconds` | `int` | 2 | Collection interval (1-60 seconds) |
| `GraphWindowMinutes` | `int` | 15 | Chart display window |
| `MaxRetentionHours` | `int` | 24 | Data retention in hours |
| `Retention` | `RetentionPreset` | OneDay | Preset retention selection |
| `RetentionInputText` | `string` | `""` | User-typed retention (e.g., `"7d"`) |
| `Theme` | `ThemeMode` | Dark | UI theme |
| `StorageDirectory` | `string` | DefaultStorageDirectory | Database storage path |
| `LaunchOnStartup` | `bool` | false | Auto-start with Windows |
| `MinimizeToTray` | `bool` | true | Minimize to system tray |
| `PanelVisibility` | `Dictionary<string, bool>` | `{}` | Per-panel show/hide state |
| `PanelOrder` | `List<string>` | `[]` | Custom panel ordering |

#### Static Properties (not serialized)

| Property | Value | Description |
|---|---|---|
| `DefaultScrapeIntervalSeconds` | 2 | Exposed default |
| `DefaultGraphWindowMinutes` | 15 | Exposed default |
| `MaxGraphWindowMinutes` | 43200 | Exposed max |
| `DefaultMaxRetentionHours` | 24 | Exposed default |
| `SettingsDirectory` | `%LocalAppData%\Vaktr` | Settings folder |
| `LegacySettingsDirectory` | `%AppData%\Vaktr` | Legacy settings folder (migration) |
| `DefaultStorageDirectory` | `%LocalAppData%\Vaktr\Data` | Default database folder |
| `LegacyDefaultStorageDirectory` | `%AppData%\Vaktr\Data` | Legacy database folder |

#### Instance Methods

| Method | Returns | Description |
|---|---|---|
| `GetDatabasePath()` | `string` | Returns `{StorageDirectory}\vaktr-metrics.db` |
| `GetRetentionWindow()` | `TimeSpan` | Parses RetentionInputText or computes from MaxRetentionHours |
| `Normalize()` | `VaktrConfig` | Clamps all values to valid ranges, returns `this` |

#### Static Methods

| Method | Returns | Description |
|---|---|---|
| `GetConfigPath()` | `string` | Returns `{SettingsDirectory}\vaktr-settings.json` |
| `GetLegacyConfigPath()` | `string` | Returns `{LegacySettingsDirectory}\vaktr-settings.json` |
| `CreateDefault()` | `VaktrConfig` | Factory for default configuration |
| `FormatRetentionInput(int hours)` | `string` | Formats as `"Xd"` or `"Xh"` |
| `TryParseRetentionWindow(string?, out TimeSpan, out string)` | `bool` | Parses `"Xm"`, `"Xh"`, or `"Xd"` format |

#### Normalize() Behavior
Clamps all properties to valid ranges:
- `ScrapeIntervalSeconds`: 1-60 (default: 2)
- `GraphWindowMinutes`: 1-43200 (default: 15)
- `StorageDirectory`: falls back to default if empty/whitespace
- `MaxRetentionHours`: non-negative
- Ensures storage directory is a valid path

---

## 6. Vaktr.Collector - Telemetry Collection

Windows-specific layer that samples hardware metrics using PDH performance counters, WMI, kernel32 APIs, and LibreHardwareMonitor.

### WindowsMetricCollector.cs

`sealed class WindowsMetricCollector : IMetricCollector`

The main workhorse that collects all system metrics. On construction, it opens a PDH query and registers performance counters. On each `CollectAsync` call, it samples all registered counters and returns a `MetricSnapshot`.

#### PDH Counters Registered

| Counter Path | Measures |
|---|---|
| `\Processor(_Total)\% Processor Time` | Total CPU usage |
| `\Processor(*)\% Processor Time` | Per-core CPU usage (array) |
| `\Processor Information(_Total)\Processor Frequency` | CPU frequency |
| `\PhysicalDisk(_Total)\Disk Read Bytes/sec` | Disk read throughput |
| `\PhysicalDisk(_Total)\Disk Write Bytes/sec` | Disk write throughput |
| `\Network Interface(*)\Bytes Received/sec` | Network receive (array) |
| `\Network Interface(*)\Bytes Sent/sec` | Network send (array) |
| `\GPU Engine(*)\Utilization Percentage` | GPU utilization (array) |
| `\GPU Process Memory(*)\Dedicated Usage` | GPU VRAM usage (array) |

#### Refresh Intervals (cached metric stales)

| Metric | Refresh Interval | Description |
|---|---|---|
| Drive usage (capacity) | 60 seconds | Disk space doesn't change rapidly |
| Host activity (process/thread/handle counts) | 75 seconds | Aggregate system counts |
| Process activity (per-process CPU/memory) | 10 seconds | Per-process breakdown |
| Temperature sensors | 20 seconds | Hardware temperature readings |

#### Private Collection Methods

| Method | Panel Key(s) | Category | Unit |
|---|---|---|---|
| `AddCpuUsage` | `cpu-total`, `cpu-per-core` | Cpu | Percent |
| `AddCpuUsageFallback` | `cpu-total` | Cpu | Percent (via GetSystemTimes) |
| `AddCpuFrequency` | `cpu-frequency` | Cpu | Megahertz |
| `AddMemory` | `memory` | Memory | Gigabytes |
| `AddDisk` | `disk-throughput` | Disk | MegabytesPerSecond |
| `AddDriveUsage` | `drive-{letter}` | Disk | Gigabytes |
| `AddNetwork` | `network` | Network | MegabitsPerSecond |
| `AddNetworkFallback` | `network` | Network | MegabitsPerSecond (via NetworkInterface) |
| `AddGpu` | `gpu-utilization`, `gpu-memory` | Gpu | Percent, Gigabytes |
| `AddTemperatures` | `gpu-temperature` | Gpu | Celsius |
| `AddHostActivity` | `host-activity` | System | Count |

#### Nested Types

| Type | Kind | Description |
|---|---|---|
| `MemoryStatusEx` | struct | Win32 MEMORYSTATUSEX for GlobalMemoryStatusEx |
| `FileTime` | struct | Win32 FILETIME with `ToUInt64()` conversion |
| `NetworkBaseline` | readonly record struct | Tracks bytes received/sent at a timestamp for delta calculation |
| `ProcessCpuBaseline` | readonly record struct | Tracks process CPU time at a timestamp for delta calculation |
| `CachedMetricValue` | readonly record struct | Stores a cached metric sample for infrequently-refreshed metrics |

#### Process Enumeration

Uses `CreateToolhelp32Snapshot` + `Process32First/Next` to enumerate processes. For each process:
1. Opens process handle with `PROCESS_QUERY_LIMITED_INFORMATION`
2. Calls `GetProcessTimes` to measure kernel + user CPU time
3. Calls `GetProcessMemoryInfo` for working set
4. Calls `GetProcessHandleCount` for handle count
5. Computes CPU% by comparing against a stored baseline (previous sample)

Process names are normalized: the `.exe` suffix is stripped, and `svchost` processes are disambiguated by PID.

---

### CollectorService.cs

`sealed class CollectorService : IAsyncDisposable`

Orchestrates the periodic collection loop. Wraps an `IMetricCollector` and `IMetricStore`.

#### Fields

| Field | Type | Description |
|---|---|---|
| `InitialCollectionTimeout` | `TimeSpan` (3s) | Timeout for the very first collection |
| `RecurringCollectionTimeout` | `TimeSpan` (5s) | Timeout for subsequent collections |
| `_gate` | `SemaphoreSlim(1,1)` | Thread-safe start/stop coordination |
| `_loopCancellation` | `CancellationTokenSource?` | Cancels the collection loop |
| `_timer` | `PeriodicTimer?` | Ticks at the configured scrape interval |
| `_loopTask` | `Task?` | The background collection loop task |

#### Events

| Event | Type | Description |
|---|---|---|
| `SnapshotCollected` | `EventHandler<MetricSnapshot>` | Fired after each successful collection |
| `CollectionFailed` | `EventHandler<Exception>` | Fired when a collection attempt fails |

#### Methods

| Method | Description |
|---|---|
| `StartAsync(VaktrConfig, CancellationToken)` | Stops any existing loop, creates a new PeriodicTimer, starts RunLoopAsync |
| `StopAsync()` | Cancels the loop, waits for it to finish, disposes timer |
| `DisposeAsync()` | Calls StopAsync, disposes the collector and store |

#### Collection Loop (`RunLoopAsync`)

1. Performs an initial collection immediately with 3s timeout
2. Enters `while (await timer.WaitForNextTickAsync())` loop
3. Each tick calls `CollectOnceAsync` with 5s timeout
4. On success: appends snapshot to store, raises `SnapshotCollected`
5. On failure: raises `CollectionFailed` (does not stop the loop)
6. On cancellation: exits cleanly

---

### TemperatureSensorReader.cs

`internal sealed class TemperatureSensorReader : IDisposable`

Reads CPU and GPU temperatures using LibreHardwareMonitor, with WMI fallback for CPU.

#### Initialization
Lazily initializes a `LibreHardwareMonitorLib.Computer` with CPU and GPU sensors enabled. If initialization fails, `_initFailed` is set to prevent repeated attempts.

#### `Read()` Method
Returns a `TemperatureReading` record:
- Iterates all hardware and sub-hardware
- Classifies temperature sensors as CPU or GPU based on hardware type
- Falls back to WMI `MSAcpi_ThermalZoneTemperature` if LibreHardwareMonitor finds no CPU temps
- Returns average CPU temp, average GPU temp, and list of discovered sensor names

#### Nested Types

| Type | Description |
|---|---|
| `TemperatureReading` | sealed record: `CpuTemperatureCelsius`, `GpuTemperatureCelsius`, `Sensors[]` |
| `UpdateVisitor` | IVisitor that calls `Update()` on all hardware to refresh sensor values |

---

### Interop/PdhNative.cs

`internal static class PdhNative`

P/Invoke declarations for the Windows Performance Data Helper (PDH) API.

#### Constants

| Constant | Value | Description |
|---|---|---|
| `ErrorSuccess` | 0 | Success status code |
| `PdhMoreData` | 0x800007D2 | Buffer too small, more data available |
| `PdhFmtDouble` | 0x00000200 | Return counter values as doubles |
| `PdhFmtNoCap100` | 0x00008000 | Don't cap percentage values at 100% |

#### P/Invoke Functions

| Function | Description |
|---|---|
| `PdhOpenQuery` | Opens a new PDH query handle |
| `PdhAddEnglishCounter` | Adds a performance counter using English name (locale-independent) |
| `PdhCollectQueryData` | Collects current data for all counters in the query |
| `PdhCloseQuery` | Closes a query handle |
| `PdhGetFormattedCounterValue` | Gets a single formatted counter value |
| `PdhGetFormattedCounterArray` | Gets an array of formatted values (wildcard counters) |

#### Structs

| Struct | Description |
|---|---|
| `PdhFmtCounterValueDouble` | Counter value with status code and double value |
| `PdhFmtCounterValueItemDouble` | Named counter value (for array results) |

---

### Interop/ProcessNative.cs

`internal static class ProcessNative`

P/Invoke declarations for Windows process enumeration and inspection via kernel32.

#### Constants

| Constant | Value | Description |
|---|---|---|
| `Th32csSnapProcess` | 0x00000002 | Snapshot flag for processes |
| `ProcessQueryLimitedInformation` | 0x1000 | Process access right |
| `ProcessVmRead` | 0x0010 | Process VM read access |
| `InvalidHandleValue` | -1 | Invalid handle sentinel |

#### P/Invoke Functions

| Function | Description |
|---|---|
| `CreateToolhelp32Snapshot` | Creates a snapshot of processes/threads |
| `Process32First` / `Process32Next` | Iterates process entries in the snapshot |
| `OpenProcess` | Opens a process handle with specified access |
| `CloseHandle` | Closes any kernel object handle |
| `GetProcessHandleCount` | Queries number of handles owned by a process |
| `GetProcessTimes` | Gets creation, exit, kernel, and user times |
| `GetProcessMemoryInfo` | Gets working set and page file usage |

#### Structs

| Struct | Description |
|---|---|
| `ProcessEntry32` | Process snapshot entry (PID, thread count, parent PID, exe name) |
| `FileTime` | Win32 FILETIME with `ToUInt64()` |
| `ProcessMemoryCounters` | Working set, page file, peak values |

---

## 7. Vaktr.Store - Persistence Layer

Handles all data persistence: metric history in SQLite and configuration in JSON.

### Persistence/SqliteMetricStore.cs

`sealed class SqliteMetricStore : IMetricStore`

#### Database Configuration

| PRAGMA | Value | Purpose |
|---|---|---|
| `journal_mode` | WAL | Write-Ahead Logging for concurrent reads |
| `synchronous` | NORMAL | Balance between safety and performance |
| `temp_store` | MEMORY | In-memory temp tables |
| `auto_vacuum` | INCREMENTAL | Gradual space reclamation |

#### Schema

**Table: `metric_samples`** (raw data, retains last 6 hours)

| Column | Type | Description |
|---|---|---|
| `panel_key` | TEXT NOT NULL | Panel identifier |
| `panel_title` | TEXT NOT NULL | Display name |
| `series_key` | TEXT NOT NULL | Series identifier |
| `series_name` | TEXT NOT NULL | Display name |
| `category` | INTEGER NOT NULL | MetricCategory enum value |
| `unit` | INTEGER NOT NULL | MetricUnit enum value |
| `timestamp_ms` | INTEGER NOT NULL | Unix epoch milliseconds |
| `value` | REAL NOT NULL | Metric value |

**Indexes on `metric_samples`:**
- `idx_metric_samples_timestamp` on `(timestamp_ms)`
- `idx_metric_samples_panel_series_time` on `(panel_key, series_key, timestamp_ms)`

**Table: `metric_rollups_1m`** (1-minute averaged aggregates, for data older than 6 hours)

Same schema as `metric_samples`.

**Indexes on `metric_rollups_1m`:**
- `idx_metric_rollups_identity` UNIQUE on `(panel_key, series_key, timestamp_ms)`
- `idx_metric_rollups_timestamp` on `(timestamp_ms)`

#### Key Constants

| Constant | Value | Purpose |
|---|---|---|
| `RawResolutionWindow` | 6 hours | Raw data kept at full resolution |
| `MaintenanceInterval` | 15 minutes | How often compaction/pruning runs |
| `MinuteBucketMilliseconds` | 60,000 | Bucket size for 1-minute rollups |

#### Methods

| Method | Description |
|---|---|
| `InitializeAsync` | Creates schema, sets PRAGMAs, prepares insert command |
| `AppendSnapshotAsync` | Inserts all samples in a transaction, triggers maintenance if due |
| `LoadHistoryAsync` | UNION query: rollups (older) + raw samples (recent), grouped by panel/series |
| `PruneAsync` | Forces compaction and pruning outside the regular schedule |
| `DisposeAsync` | Checkpoints WAL (`TRUNCATE` mode), closes connections |

#### Compaction & Pruning (`CompactAndPruneAsync`)

1. If retention > 6 hours:
   - Compacts raw samples older than 6 hours into 1-minute rollups (`INSERT OR REPLACE ... GROUP BY ... AVG(value)`)
   - Deletes raw samples older than 6 hours
   - Deletes rollup data older than retention window
2. If retention <= 6 hours:
   - Deletes raw samples older than retention
3. Runs `PRAGMA optimize`, `PRAGMA wal_checkpoint(PASSIVE)`, `PRAGMA incremental_vacuum(64)`

#### Nested Types

| Type | Description |
|---|---|
| `PanelAccumulator` | Groups series by panel during history loading |
| `SeriesAccumulator` | Collects `MetricPoint` lists for a single series |

---

### Persistence/JsonConfigStore.cs

`sealed class JsonConfigStore : IConfigStore`

#### Serialization Options
- `WriteIndented = true`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`

#### Methods

| Method | Description |
|---|---|
| `LoadAsync` | Reads `vaktr-settings.json`, deserializes, normalizes. Falls back to legacy path, then default config. |
| `SaveAsync` | Normalizes config, creates directories, serializes to JSON. |

#### Path Resolution
1. Checks `%LocalAppData%\Vaktr\vaktr-settings.json` (current path)
2. Falls back to `%AppData%\Vaktr\vaktr-settings.json` (legacy path) if current doesn't exist
3. Returns current path as default write location

---

## 8. Vaktr.App - WinUI 3 Desktop Application

The presentation layer built entirely with WinUI 3. Uses MVVM pattern with custom canvas-rendered controls.

### Program.cs - Entry Point

`static class Program` with `[STAThread] Main(string[] args)`

**Startup sequence:**
1. Writes startup trace to `%LocalAppData%\Vaktr\startup-trace.log`
2. Registers global `AppDomain.UnhandledException` handler
3. Registers `TaskScheduler.UnobservedTaskException` handler
4. Initializes WinRT COM wrappers (`ComWrappersSupport.InitializeComWrappers()`)
5. Creates `DispatcherQueueController` for the main thread
6. Sets `SynchronizationContext` to `DispatcherQueueSynchronizationContext`
7. Instantiates `App` (triggers `OnLaunched`)

---

### App.cs - Application Lifecycle

`sealed class App : Application`

#### Key Methods

| Method | Description |
|---|---|
| `OnLaunched` | Loads config from JsonConfigStore, creates MainViewModel, SqliteMetricStore, applies theme, creates ShellWindow |
| `ApplyTheme(ThemeMode)` | Sets all 18 theme brush resources, refreshes window chrome and all controls |
| `PreviewTheme(ThemeMode)` | Temporarily applies theme for preview (settings editing) |
| `ApplyThemeResources(ThemeMode)` | Populates `Application.Current.Resources` with the 18 named brushes |
| `MarkStartupSettled()` | Disarms startup guard after UI is fully loaded |

#### Theme Palette (18 named brushes)

| Brush Name | Dark Mode | Light Mode | Purpose |
|---|---|---|---|
| `AppBackdropBrush` | #030812 | #EEF5FB | Window background |
| `ShellBackgroundBrush` | #07101B | #F8FBFF | Shell container |
| `ShellStrokeBrush` | #28445E | #C4D2E2 | Shell border |
| `SurfaceBrush` | #0C1726 | #FFFFFF | Card surfaces |
| `SurfaceElevatedBrush` | #112033 | #F0F5FB | Subtle card elevation |
| `SurfaceStrongBrush` | #18314A | #E4EDF6 | Buttons, hover states |
| `SurfaceStrokeBrush` | #315274 | #B8C9DC | Card borders |
| `PanelOverlayBrush` | #091321 | #E6EFF8 | Panel overlay |
| `SurfaceGridBrush` | #426A8C | #C8D8E8 | Chart grid lines |
| `TextPrimaryBrush` | #F4F9FF | #0A1824 | Primary text |
| `TextSecondaryBrush` | #C6D7EA | #3A5068 | Secondary text |
| `TextMutedBrush` | #8098B2 | #576F85 | Muted/disabled text |
| `AccentBrush` | #6FE2FF | #0975A8 | Primary accent |
| `AccentStrongBrush` | #C7F5FF | #065A82 | Strong emphasis |
| `AccentSoftBrush` | #163B60 | #C8E8F8 | Soft backgrounds |
| `AccentHaloBrush` | #2B8FE6C4 | #1C7EA229 | Glow effects |
| `WarningHaloBrush` | #1EFFA25C | #19D2A04A | Warning indicators |
| `OverlayScrimBrush` | #B0060A12 | #90E0EBF5 | Modal overlays |

---

### ShellWindow.Polished.cs - Main Window Logic

`sealed class ShellWindow : Window`

The main application window. Manages the entire dashboard UI, settings panel, telemetry subscription, and user interactions.

#### Static Presets

| Preset | Values | Description |
|---|---|---|
| `ScrapeIntervalPresets` | [1, 2, 5, 10, 15, 30, 60] | Seconds between collections |
| `RetentionHourPresets` | [1, 6, 12, 24, 48, 72, 168, 336, 720, 2160, 8760] | Hours of data retention |

#### Major UI Sections

1. **Title Bar** - Custom draggable title bar with window chrome
2. **Header** - Brand image (96x96) + "Vaktr" title (52pt) + subtitle
3. **At-A-Glance** - 4 summary cards (CPU, GPU, RAM, Drives) with mini gauges
4. **Live Board** - Dashboard grid of TelemetryPanelCards with global range controls
5. **Control Deck** - Settings panel (scrape interval, retention, storage, theme)
6. **Status Footer** - Current status text

#### Key Event Handlers

| Handler | Trigger | Description |
|---|---|---|
| `OnRootLoaded` | First paint | Starts telemetry, loads history, marks startup settled |
| `OnWindowClosed` | Window close | Disposes collector, store, handles |
| `OnWindowActivated` | Focus change | Refreshes chrome, applies icon |
| `OnSnapshotCollected` | New telemetry | Queues snapshot for UI application |
| `OnCollectionFailed` | Collector error | Updates status text |
| `OnSaveSettingsClick` | Apply button | Validates inputs, saves config, restarts collector if needed |
| `OnThemeQuickToggle` | Theme toggle | Previews/applies theme change |
| `OnToggleGlobalRangeEditor` | Range button | Shows/hides time range editor panel |
| `OnGlobalWindowRangeClick` | Range preset | Applies selected time window globally |
| `OnPanelDragEnded` | Panel drag | Reorders panels in grid |
| `OnScrollHostViewChanged` | Scroll | Suspends panel rendering during scroll |
| `OnRootLayoutSizeChanged` | Window resize | Recalculates responsive grid columns |

#### Settings Validation

| Setting | Input Format | Valid Range |
|---|---|---|
| Scrape interval | Integer seconds | 1-60 |
| Retention | `"Xm"`, `"Xh"`, `"Xd"` | 30 minutes - unlimited |
| Storage directory | File path | Must be valid directory path |
| Theme | Dark / Light toggle | - |

#### Responsive Breakpoints

| Width | Dashboard Columns | Summary Columns |
|---|---|---|
| < 900px | 1 | 1 |
| 900-1259px | 2 | 2 |
| >= 1260px | 3 | 4 |

---

### ShellWindow.Polished.Ui.cs - UI Construction

Partial class containing all UI building methods for ShellWindow.

#### Key Builder Methods

| Method | Returns | Description |
|---|---|---|
| `BuildLoadingScreen()` | Grid | Ultra-lightweight loading overlay with 3 pulsing cyan dots |
| `BuildRootLayout()` | Grid | Main layout: title bar row + scrollable content row |
| `BuildShellStack()` | StackPanel | Vertical stack of all sections (header, summary, board, controls) |
| `BuildHeader()` | FrameworkElement | Brand image + "Vaktr" title + subtitle |
| `BuildSummaryCards()` | void | Creates 4 summary card visuals with icons, values, gauges |
| `BuildControlsSurface()` | Border | Settings container with header and body |
| `RenderControlDeckSummary()` | void | Read-only settings display (3 columns) |
| `RenderEditableControlDeck()` | void | Editable settings form with 3 editor cards |
| `RenderGlobalRangeEditor()` | void | Time range picker with presets and absolute date inputs |

#### Summary Cards (At-A-Glance)

Each of the 4 summary cards displays:
- **Icon tile** with category-specific color
- **Title** (spaced, muted text)
- **Value** (large, primary text - e.g., "45.2%")
- **Gauge track** (thin horizontal bar)
- **Gauge fill** (colored: cyan normal, yellow >75%, orange >90%)
- **Caption** (secondary info - e.g., "3.5 GHz / 128 proc")

| Card | Title | Metrics Shown |
|---|---|---|
| CPU | CPU | Usage %, frequency, process count |
| GPU | GPU | Usage %, VRAM |
| RAM | MEMORY | Used/total GB |
| Drive | DRIVES | Highest utilization % |

#### Control Deck Editor Cards

| Card | Accent Color | Controls |
|---|---|---|
| Collection | #5DE6FF (cyan) | Interval input + presets (1s, 2s, 5s, 10s, 15s) |
| Retention | #67B7FF (blue) | Retention input + presets (6h, 12h, 24h, 7d, 30d, 90d) |
| Storage | #6EE7C8 (teal) | Path input + Browse + Use Default |

#### Text Factories

| Method | Font | Color Brush |
|---|---|---|
| `CreatePrimaryText` | Segoe UI Variable Display/Text | TextPrimaryBrush |
| `CreateSecondaryText` | Segoe UI Variable Text | TextSecondaryBrush |
| `CreateMutedText` | Segoe UI Variable Text | TextMutedBrush |
| `CreateAccentText` | Segoe UI Variable Text | AccentBrush |

#### Surface Gradient Cache
Gradients are cached by hex key pair. On theme change, cache is invalidated. In light mode, dark hex values are automatically mapped to lighter equivalents via `LiftToLight()`.

---

### ViewModels/ObservableObject.cs

`abstract class ObservableObject : INotifyPropertyChanged`

Base class for all ViewModels. Provides:
- `PropertyChanged` event
- `SetProperty<T>(ref T, T, [CallerMemberName])` - sets value if changed, raises event
- `RaisePropertyChanged([CallerMemberName])` - manually raises PropertyChanged

---

### ViewModels/DashboardViewModels.cs

#### `MainViewModel : ObservableObject`

Central orchestrator for all dashboard state.

**Properties:**

| Property | Type | Description |
|---|---|---|
| `DashboardPanels` | `ObservableCollection<MetricPanelViewModel>` | Visible panels |
| `PanelToggles` | `ObservableCollection<PanelToggleViewModel>` | Panel visibility toggles |
| `SummaryCards` | `IReadOnlyList<SummaryCardViewModel>` | 4 summary cards |
| `StorageDirectory` | `string` | Custom storage path |
| `ScrapeIntervalInput` | `string` | User-typed interval |
| `SelectedIntervalSeconds` | `int` | Active scrape interval |
| `SelectedWindowMinutes` | `int` | Active chart time window |
| `SelectedTheme` | `ThemeMode` | Active theme |
| `LaunchOnStartup` | `bool` | Auto-start flag |
| `MinimizeToTray` | `bool` | Tray minimize flag |
| `RetentionHoursInput` | `string` | User-typed retention |

**Key Methods:**

| Method | Description |
|---|---|
| `BuildConfig()` | Constructs VaktrConfig from current ViewModel state |
| `ApplyConfig(VaktrConfig)` | Loads config into ViewModel properties |
| `LoadHistory(IReadOnlyList<MetricSeriesHistory>)` | Creates/updates panels from historical data |
| `ApplySnapshot(MetricSnapshot)` | Appends new samples to each panel, updates summaries |
| `ApplyPanelVisibility()` | Syncs panel visibility from toggles |
| `ApplyGlobalWindowRange(int)` | Sets all panels to the same time window |
| `ApplyGlobalAbsoluteRange(start, end)` | Pins all panels to an absolute time range |
| `ResetGlobalZoom()` | Clears absolute range, returns to rolling window |
| `MovePanel(movingKey, targetKey)` | Swaps two panels in the display order |
| `UpdateSummaryCards(MetricSnapshot, PanelDetailContext)` | Updates the 4 summary card values |

#### `MetricPanelViewModel : ObservableObject`

Represents a single telemetry panel on the dashboard (e.g., "CPU Total", "GPU Temperature").

**Identity Properties:**

| Property | Description |
|---|---|
| `PanelKey` | Unique identifier (e.g., `"cpu-total"`, `"drive-c"`) |
| `Title` | Display title |
| `Category` | MetricCategory enum |
| `Unit` | MetricUnit enum |
| `Badge` | Short label: "CPU", "GPU", "MEM", "DRV", "DSK", "NET", "SYS" |
| `AccentBrush` | Color-coded by category |

**Display Properties:**

| Property | Description |
|---|---|
| `VisibleSeries` | Chart line series for rendering |
| `CurrentValue` | Formatted latest value (e.g., "45.2%") |
| `SecondaryValue` | Additional context value |
| `FooterText` | Unit/context note |
| `ScaleLabel` | Y-axis max label |
| `ChartCeilingValue` | Max Y-axis value |
| `UtilizationPercent` | Current utilization for gauges |
| `GaugeValue` | Last value clamped 0-100 |

**Process Detail Properties (CPU/Memory panels only):**

| Property | Description |
|---|---|
| `SupportsProcessTable` | Whether this panel shows per-process data |
| `ProcessRows` | List of top processes |
| `ProcessSortMode` | Highest / Lowest / Name |
| `ProcessListExpanded` | Show more processes |
| `PerProcessChartsEnabled` | Overlay per-process lines on chart |

**Sorting:**

| Property | Description |
|---|---|
| `SortBucket` | Primary sort: CPU=0, GPU=1, Memory=2, Disk=3, Network=4, Other=5 |
| `SortGroupKey` | Groups related panels (e.g., all drives) |
| `SortVariant` | Secondary sort within group (throughput before volumes) |

**Key Methods:**

| Method | Description |
|---|---|
| `LoadHistory(MetricSeriesHistory)` | Populates series from historical data |
| `AppendSample(MetricSample)` | Adds a new data point to the appropriate series |
| `ApplyDetailContext(PanelDetailContext)` | Updates derived values (frequency, process counts) |
| `ApplyRangePreset(TimeRangePreset)` | Changes the visible time window |
| `ZoomToWindow(start, end)` | Pins to an absolute time range |
| `ResetZoom()` | Returns to rolling time window |
| `RefreshPresentation()` | Rebuilds visible series and display strings |
| `RefreshProcessRows()` | Rebuilds process list based on sort mode |

#### `SummaryCardViewModel : ObservableObject`

One of the 4 at-a-glance cards.

| Property | Type | Description |
|---|---|---|
| `Glyph` | `string` | Icon key ("cpu", "gpu", "memory", "disk") |
| `Title` | `string` | Display title |
| `AccentBrush` | `Brush` | Card accent color |
| `Value` | `string` | Current value (e.g., "45.2%") |
| `Caption` | `string` | Additional detail |
| `Utilization` | `double` | 0-100 for gauge fill |

#### `PanelToggleViewModel : ObservableObject`

| Property | Type | Description |
|---|---|---|
| `PanelKey` | `string` | Panel identifier |
| `Title` | `string` | Display name |
| `IsVisible` | `bool` | Checked state |

#### `PanelDetailContext`

Extracted per-snapshot enrichment data.

| Property | Type | Description |
|---|---|---|
| `CpuFrequencyMhz` | `double` | Current CPU frequency |
| `ProcessCount` | `int` | Total process count |
| `ThreadCount` | `int` | Total thread count |
| `HandleCount` | `int` | Total handle count |
| `Processes` | `IReadOnlyList<ProcessActivitySample>` | Per-process data |
| `DriveDetails` | `IReadOnlyDictionary<string, DriveDetailContext>` | Per-drive usage |

#### `ChartSeriesViewModel`

| Property | Type | Description |
|---|---|---|
| `Key` | `string` | Series identifier |
| `Name` | `string` | Display name |
| `Color` | `Color` | Line/fill color |
| `Points` | `IReadOnlyList<MetricPoint>` | Data points |

#### `ProcessSortMode` (enum)
`Highest = 0`, `Lowest = 1`, `Name = 2`

#### `ProcessListItemViewModel`

| Property | Description |
|---|---|
| `Key` | Process name key |
| `Name` | Display name |
| `Value` | Formatted usage value |
| `Caption` | Additional info |
| `Intensity` | 0-1 for bar fill width |

---

### Controls/TelemetryChart.cs

`sealed class TelemetryChart : UserControl`

A custom canvas-based line chart with time-series rendering, hover tooltips, and drag-to-zoom.

#### Dependency Properties

| Property | Type | Description |
|---|---|---|
| `Series` | `IReadOnlyList<ChartSeriesViewModel>` | Line series to render |
| `Unit` | `MetricUnit` | Y-axis unit |
| `WindowStartUtc` | `DateTimeOffset` | Visible window start |
| `WindowEndUtc` | `DateTimeOffset` | Visible window end |
| `CeilingValue` | `double` | Y-axis maximum |
| `EmptyStateText` | `string` | Placeholder text when no data |

#### Rendering Features

- **Downsampling**: Point budget based on canvas width (avoids rendering thousands of points)
- **Gap detection**: Splits line segments when time gaps exceed 3x expected interval
- **Filled area**: Semi-transparent fill under curves (opacity varies by series count)
- **Point markers**: Small dots on individual data points
- **Grid lines**: Horizontal grid with Y-axis labels
- **Hover tooltip**: Shows values at cursor position with vertical guideline
- **Selection rectangle**: Drag to select a time range for zooming
- **Double-click**: Resets zoom

#### Events

| Event | Args | Description |
|---|---|---|
| `ZoomSelectionRequested` | `ChartZoomSelectionEventArgs` (StartUtc, EndUtc) | User dragged a selection rectangle |
| `ZoomResetRequested` | `EventHandler` | User double-clicked to reset zoom |

---

### Controls/TelemetryPanelCard.cs

`sealed class TelemetryPanelCard : UserControl`

A complete metric panel card displaying chart, gauge, legend, process table, and range controls.

#### Visual Sections

1. **Header**: Badge icon + title + secondary value + drag handle
2. **Chart/Gauge area**: TelemetryChart (line) or UsageGauge (radial arc)
3. **Legend**: Color indicators + series names (scrollable if many series)
4. **Process table** (CPU/Memory only): Sortable process list with usage bars
5. **Range buttons**: Quick presets (1m, 5m, 15m, 1h)

#### Drag & Drop Reordering

Panels can be reordered by dragging the header:
1. `OnHeaderPointerDown` - Records start position
2. `OnHeaderPointerMoved` - Initiates drag after threshold, moves card visually
3. `OnHeaderPointerUp` - Hit-tests drop target, fires `PanelReorderRequested`
4. `PlaySwapSettleAnimation` - Animates panel sliding into new position

#### Rendering Optimization

| Field | Purpose |
|---|---|
| `_isRenderingSuspended` | Skips chart/legend redraws during scroll |
| `_deferredRefreshPending` | Queues a refresh for when rendering resumes |
| `_isEffectivelyVisible` | Whether the card is in the viewport |
| `_lastRendered*` | Caches to avoid redundant redraws |

#### Events

| Event | Description |
|---|---|
| `RangePresetRequested` | User clicked a range preset button |
| `PanelZoomSelectionRequested` | User dragged to zoom on the chart |
| `PanelZoomResetRequested` | User double-clicked the chart |
| `PanelReorderRequested` | User dragged panel to a new position |
| `PanelDragEnded` | Drag operation completed |

---

### Controls/UsageGauge.cs

`sealed class UsageGauge : UserControl`

A radial arc gauge showing utilization percentage.

#### Dependency Properties

| Property | Type | Description |
|---|---|---|
| `Value` | `double` | Utilization 0-100% |
| `AccentBrush` | `Brush` | Arc fill color |
| `Caption` | `string` | Text below the percentage |

#### Visual Design
- 270-degree arc sweep (starting at 135 degrees)
- Background track at reduced opacity
- Colored fill arc proportional to value
- Centered large percentage text + caption

---

### Controls/ActionChip.cs

`sealed class ActionChip : UserControl`

An interactive button/chip control used throughout the UI.

#### Properties

| Property | Description |
|---|---|
| `Text` | Button label |
| `IsActive` | Selected state (accent colored border) |
| `IsFilled` | Filled background state |

#### Visual States

| State | Appearance |
|---|---|
| Idle | Surface stroke border, transparent background |
| Hover | Slightly darker background |
| Pressed | Reduced opacity (0.82) |
| Active | Accent border, accent-soft background |
| Active + Filled | Accent background, strong text |

---

### Controls/InlineTextEntry.cs

`sealed class InlineTextEntry : UserControl`

A custom text input control with blinking cursor visual.

#### Properties

| Property | Description |
|---|---|
| `Text` | Current text value |
| `PlaceholderText` | Shown when text is empty |

#### Input Handling
- Character input: Filters control characters
- Backspace: Removes last character
- Escape: Blurs the control
- Focused state shows `|` cursor appended to text

---

### Controls/IconFactory.cs

`internal static class IconFactory`

Utility for creating themed icon tiles with Segoe Fluent Icons.

#### Glyph Mappings

| Key | Icon | Segoe Fluent Glyph |
|---|---|---|
| `cpu` | CPU | \uEEA1 |
| `memory` / `ram` | RAM | \uE964 |
| `disk` | Hard Drive | \uEDA2 |
| `network` | Network | \uE839 |
| `gpu` / `graphics` | Video | \uE714 |
| `temperature` | Thermometer | \uE9CA |
| `system` / `activity` | System | \uEC05 |
| `collection` / `clock` | Clock | \uE916 |
| `retention` / `history` | History | \uE81C |
| `storage` / `folder` | Folder | \uE8B7 |

---

### Services/AutoLaunchService.cs

`sealed class AutoLaunchService`

Manages Windows auto-start via the registry.

**Registry path:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
**Value name:** `Vaktr`

| Method | Description |
|---|---|
| `SetEnabled(true)` | Sets registry value to quoted path of current process |
| `SetEnabled(false)` | Deletes registry value |

---

### Services/StartupTrace.cs

`internal static class StartupTrace`

Writes diagnostic logs to `%LocalAppData%\Vaktr\startup-trace.log`.

| Method | Description |
|---|---|
| `Reset()` | Clears the log file on startup |
| `Write(string)` | Appends a timestamped message |
| `WriteException(string, Exception)` | Logs exception with stage context and stack trace |

---

## 9. Vaktr.Tests - Test Suite

**Framework:** xUnit 2.7.0

### VaktrConfigTests

| Test | Description |
|---|---|
| `Normalize_Resets_Out_Of_Range_Values` | Creates config with invalid values (ScrapeIntervalSeconds=0, GraphWindowMinutes=-1, StorageDirectory=""), calls Normalize(), asserts defaults are restored |

**Current coverage:** Minimal. Only tests config normalization. Collector, store, and UI logic are untested.

---

## 10. Tools - SensorProbe

A standalone diagnostic console app for probing hardware sensors.

**Location:** `tools/SensorProbe/`
**Target:** net8.0-windows
**Dependencies:** LibreHardwareMonitorLib 0.9.6, System.Management 10.0.2

### Functionality

1. **PawnIO Detection**: Checks registry at `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO` for driver installation status and version
2. **Hardware Enumeration**: Initializes `LibreHardwareMonitorLib.Computer` with CPU + GPU + Motherboard enabled, iterates all hardware, sub-hardware, and temperature sensors
3. **Output**: Prints `{HardwareType} / {HardwareName} / {SensorName}: {Value} C` for each temperature sensor

### Running

```bash
dotnet run --project tools/SensorProbe/SensorProbe.csproj
```

---

## 11. Installer - Inno Setup

**Location:** `installer/vaktr-setup.iss`
**Tool:** Inno Setup 6

### Configuration

| Setting | Value |
|---|---|
| **App ID** | `{B8F2A1C4-5D6E-4F7A-8B9C-0D1E2F3A4B5C}` |
| **Install Dir** | `{autopf}\Vaktr` (Program Files\Vaktr) |
| **Compression** | LZMA2/ultra64, solid |
| **Wizard Style** | Modern |
| **Privileges** | Admin |
| **Architecture** | x64 only |
| **Min Version** | 10.0.17763 (Windows 10 1809) |

### Optional Tasks

| Task | Description | Default |
|---|---|---|
| `desktopicon` | Create desktop shortcut | Unchecked |
| `launchonstartup` | Add to Windows startup via registry | Unchecked |

### Installer Behavior

- **Files:** Recursively copies everything from `publish\x64\`
- **Icons:** Start Menu group with app shortcut + uninstall shortcut
- **Registry:** `HKCU\...\Run\Vaktr` if startup task selected (auto-removed on uninstall)
- **Pre-install:** Checks for running instance via mutex, kills `Vaktr.exe` via `taskkill /F` if found, waits 500ms
- **Post-install:** Launches `Vaktr.exe` (unless silent install)

### Building

```bash
# First publish the app
dotnet publish Vaktr.App/Vaktr.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish/x64

# Then compile installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion="1.0.1" installer/vaktr-setup.iss
```

Output: `installer/Output/VaktrSetup.exe`

---

## 12. CI/CD - GitHub Actions

### ci.yml - Continuous Integration

**Triggers:** Pull requests to `main` and `dev` branches
**Runner:** `windows-latest`

| Step | Command |
|---|---|
| Checkout | `actions/checkout@v4` |
| Setup .NET | `actions/setup-dotnet@v4` (.NET 8.0.x) |
| Restore | `dotnet restore Vaktr.sln` |
| Build | `dotnet build Vaktr.sln -c Release -p:Platform=x64` |
| Test | `dotnet test` with TRX logger |
| Publish | `dotnet publish` self-contained win-x64 |
| Upload | Test results + build artifacts (14-day retention) |

### release.yml - Release Pipeline

**Triggers:** Push tags matching `v*`, manual workflow dispatch with version input

| Step | Command |
|---|---|
| Publish | Self-contained win-x64 to `publish/x64` |
| Install Inno Setup | `choco install innosetup -y` |
| Build Installer | `ISCC.exe /DMyAppVersion="X.Y.Z" installer/vaktr-setup.iss` |
| Upload Artifact | Installer exe |
| Create Tag | (manual dispatch only) |
| GitHub Release | Draft release with installer attached + auto-generated notes |

### security.yml - Security Scans

**Triggers:** PRs to `main`/`dev`, weekly schedule (Monday 8am UTC)

| Job | Runner | Checks |
|---|---|---|
| **Dependency Audit** | windows-latest | `dotnet list --vulnerable --include-transitive`, fails on Critical/High |
| **Secrets Scan** | ubuntu-latest | Gitleaks full repo history scan |
| **Code Quality** | windows-latest | Build with analyzers, check for outdated packages |

---

## 13. Data Flow & Runtime Architecture

### Startup Sequence

```
1. Program.Main()
   ├── StartupTrace.Reset() + Write()
   ├── Register global exception handlers
   ├── ComWrappersSupport.InitializeComWrappers()
   ├── Create DispatcherQueueController
   └── new App()

2. App.OnLaunched()
   ├── JsonConfigStore.LoadAsync() → VaktrConfig
   ├── new MainViewModel()
   ├── new SqliteMetricStore()
   ├── ApplyThemeResources(config.Theme)
   └── new ShellWindow(viewModel, metricStore, configStore)

3. ShellWindow constructor
   ├── BuildLoadingScreen() → 3 pulsing dots
   └── Queue BuildFullUi() on loaded event

4. BuildFullUi()
   ├── BuildRootLayout() → shell structure
   ├── BuildSummaryCards() → 4 summary cards
   ├── BuildControlsSurface() → settings panel
   └── Set Content = root layout

5. OnRootLoaded()
   ├── StartTelemetryAsync(config)
   │   ├── new WindowsMetricCollector()
   │   │   ├── PdhOpenQuery()
   │   │   └── PdhAddEnglishCounter() × N
   │   ├── new CollectorService(collector, store)
   │   ├── SqliteMetricStore.InitializeAsync()
   │   └── CollectorService.StartAsync()
   ├── MetricStore.LoadHistoryAsync() → populate charts
   └── App.MarkStartupSettled()
```

### Telemetry Loop (every N seconds)

```
1. PeriodicTimer ticks
2. CollectorService.CollectOnceAsync()
   ├── WindowsMetricCollector.CollectAsync()
   │   ├── PdhCollectQueryData() → refresh all counters
   │   ├── AddCpuUsage() → cpu-total, cpu-per-core panels
   │   ├── AddCpuFrequency() → cpu-frequency panel
   │   ├── AddMemory() → memory panel (GlobalMemoryStatusEx)
   │   ├── AddDisk() → disk-throughput panel
   │   ├── AddDriveUsage() → drive-{letter} panels (cached 60s)
   │   ├── AddNetwork() → network panel
   │   ├── AddGpu() → gpu-utilization, gpu-memory panels
   │   ├── AddTemperatures() → gpu-temperature panel (cached 20s)
   │   └── AddHostActivity() → host-activity panel (cached 75s)
   │       └── EnumerateProcesses() → per-process CPU/memory/threads/handles
   │   → Returns MetricSnapshot
   │
   ├── SqliteMetricStore.AppendSnapshotAsync(snapshot)
   │   ├── BEGIN TRANSACTION
   │   ├── INSERT INTO metric_samples (per sample)
   │   ├── COMMIT
   │   └── MaybeMaintainAsync() (every 15 min)
   │       ├── Compact raw → 1-minute rollups
   │       ├── DELETE old raw data
   │       ├── DELETE old rollup data
   │       └── PRAGMA optimize/checkpoint/vacuum
   │
   └── Raise SnapshotCollected event

3. ShellWindow.OnSnapshotCollected()
   └── Queue ApplyQueuedSnapshot() on DispatcherQueue (low priority)

4. ApplyQueuedSnapshot()
   ├── MainViewModel.ApplySnapshot(snapshot)
   │   ├── For each sample → panel.AppendSample()
   │   ├── panel.RefreshPresentation() → rebuild visible series
   │   └── UpdateSummaryCards() → update 4 summary values
   ├── Update TelemetryPanelCards → Chart.Redraw()
   ├── Update summary card visuals (values, gauges)
   └── UpdateStatusText()
```

### Settings Change Flow

```
1. User edits Control Deck (scrape, retention, storage, theme)
2. OnSaveSettingsClick()
   ├── Validate all inputs
   ├── MainViewModel.ApplyConfig(newConfig)
   ├── JsonConfigStore.SaveAsync(config)
   │   └── Write %LocalAppData%\Vaktr\vaktr-settings.json
   ├── If scrape interval or storage changed:
   │   └── CollectorService.StartAsync(newConfig) → restarts collection
   ├── If retention lowered:
   │   └── SqliteMetricStore.PruneAsync(config) → delete old data
   ├── If theme changed:
   │   └── App.ApplyTheme(mode) → refresh all 18 brushes
   └── AutoLaunchService.SetEnabled(config.LaunchOnStartup)
```

---

## 14. Theme System

### Architecture

The theme system uses 18 named `SolidColorBrush` resources stored in `Application.Current.Resources`. All UI elements reference these brushes by name. Theme changes swap all 18 brushes simultaneously.

### Palette Structure

**Surface hierarchy** (background to foreground):
`AppBackdrop → ShellBackground → Surface → SurfaceElevated → SurfaceStrong`

**Text hierarchy** (strong to weak):
`TextPrimary → TextSecondary → TextMuted`

**Accent hierarchy:**
`AccentStrong → Accent → AccentSoft → AccentHalo`

### Light Mode Gradient Mapping

When creating surface gradients in light mode, dark hex values are automatically mapped to lighter equivalents:

| Dark Hex | Light Equivalent |
|---|---|
| #0C1726 | #FFFFFF |
| #07101B | #F8FBFF |
| #112033 | #F0F5FB |
| #18314A | #E4EDF6 |
| #091321 | #E6EFF8 |
| Others | #F5F8FC (fallback) |

### Theme Refresh Flow

```
App.ApplyTheme(mode)
├── ApplyThemeResources(mode) → set 18 brush resources
├── ShellWindow.ApplyTheme(mode)
│   ├── Set window RequestedTheme
│   ├── Refresh window chrome (title bar colors)
│   ├── Clear gradient cache
│   ├── Refresh all TelemetryPanelCards
│   ├── Refresh all ActionChips
│   ├── Refresh all InlineTextEntrys
│   └── Rebuild control deck and summary cards
```

---

## 15. Data Paths & Storage

### File Locations

| Purpose | Path | Format |
|---|---|---|
| Settings | `%LocalAppData%\Vaktr\vaktr-settings.json` | JSON (camelCase, indented) |
| Database | `%LocalAppData%\Vaktr\Data\vaktr-metrics.db` | SQLite WAL |
| Startup trace | `%LocalAppData%\Vaktr\startup-trace.log` | Text (timestamped lines) |
| Legacy settings | `%AppData%\Vaktr\vaktr-settings.json` | JSON (migration source) |
| Legacy database | `%AppData%\Vaktr\Data\vaktr-metrics.db` | SQLite (migration source) |

### Settings JSON Structure

```json
{
  "scrapeIntervalSeconds": 2,
  "graphWindowMinutes": 15,
  "maxRetentionHours": 24,
  "retention": 1,
  "retentionInputText": "24h",
  "theme": 0,
  "storageDirectory": "",
  "launchOnStartup": false,
  "minimizeToTray": true,
  "panelVisibility": {
    "cpu-total": true,
    "gpu-utilization": true
  },
  "panelOrder": ["cpu-total", "gpu-utilization", "memory"]
}
```

### Database Data Lifecycle

```
Collection (2s intervals)
  → metric_samples (raw, full resolution)
  → After 6 hours: compacted to metric_rollups_1m (1-minute averages)
  → After retention window: deleted entirely
```

| Data Age | Storage | Resolution |
|---|---|---|
| 0 - 6 hours | `metric_samples` | Full (every scrape interval) |
| 6 hours - retention limit | `metric_rollups_1m` | 1-minute averages |
| Beyond retention | Deleted | - |

Maintenance runs every 15 minutes during active collection.
