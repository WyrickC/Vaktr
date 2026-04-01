# Vaktr

![Vaktr logo](Vaktr.App/Assets/vaktr.png)

Vaktr is a local-first Windows telemetry dashboard for people who want a clean, fast read on their machine without turning their desktop into a control room.

It keeps the collector and storage pipeline simple:
- `Vaktr.Collector` samples Windows performance counters.
- `Vaktr.Store` saves a lightweight SQLite history and app settings.
- `Vaktr.App` is the WinUI 3 desktop shell.

## What It Aims For

- Sleek, gamer-friendly hardware monitoring
- Fast startup and low overhead
- Local-only storage
- Out-of-the-box time-series charts for CPU, memory, disk I/O, and network
- Drive-usage gauges for local volumes
- Optional controls for scrape interval, retention, and storage path
- Smart local storage that keeps recent samples sharp and compacts older history automatically

## Run It

From PowerShell:

```powershell
cd C:\Repos\Vaktr
dotnet restore .\Vaktr.sln
dotnet run --project .\Vaktr.App\Vaktr.App.csproj -p:Platform=x64
```

The built desktop process is `Vaktr.exe`.

From Visual Studio:

1. Open [Vaktr.sln](C:/Repos/Vaktr/Vaktr.sln).
2. Set `Vaktr.App` as the startup project.
3. Choose `Debug` and `x64`.
4. Press `F5`.

## Where Data Lives

- Settings: `%LocalAppData%\Vaktr\vaktr-settings.json`
- Metrics database: `%LocalAppData%\Vaktr\Data\vaktr-metrics.db`

## Defaults

- Scrape interval defaults to `2` seconds if you leave the setting blank.
- Max retention defaults to `24` hours if you leave the setting blank.
- Storage defaults to `%LocalAppData%\Vaktr\Data` if you leave the path blank.

## Notes

- The app is designed to stay responsive by deferring telemetry startup until after the first paint, keeping only a bounded in-memory live window, and persisting history to SQLite.
- Recent samples stay at full resolution for a short local window, and older data is compacted into 1-minute rollups so longer retention remains practical.
- The WinUI shell uses lightweight code-only controls for charts and gauges so it stays fast without extra runtime dependencies.
- Closing can minimize to the tray if that option is enabled in the control deck.
- If the solution has stale Visual Studio cache issues, close Visual Studio and delete the `.vs` folder once before reopening the solution.
