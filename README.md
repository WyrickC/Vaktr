# Vaktr

Vaktr is a local-first Windows telemetry dashboard for people who want a clean, fast read on their machine without turning their desktop into a control room.

It keeps the collector and storage pipeline simple:
- `Vaktr.Collector` samples Windows performance counters.
- `Vaktr.Store` saves a lightweight SQLite history and app settings.
- `Vaktr.App` is the WinUI 3 desktop shell.

## What It Aims For

- Sleek, gamer-friendly hardware monitoring
- Fast startup and low overhead
- Local-only storage
- Simple controls for interval, retention, theme, startup, and visible panels

## Run It

From PowerShell:

```powershell
cd C:\Repos\Vaktr
dotnet restore .\Vaktr.sln
dotnet run --project .\Vaktr.App\Vaktr.App.csproj -p:Platform=x64
```

From Visual Studio:

1. Open [Vaktr.sln](C:/Repos/Vaktr/Vaktr.sln).
2. Set `Vaktr.App` as the startup project.
3. Choose `Debug` and `x64`.
4. Press `F5`.

## Where Data Lives

- Settings: `%AppData%\Vaktr\vaktr-settings.json`
- Metrics database: `%AppData%\Vaktr\Data\vaktr-metrics.db`

## Notes

- The app is designed to stay responsive by keeping sampling local, retaining only a bounded in-memory window for live charts, and persisting history to SQLite.
- Closing can minimize to the tray if that option is enabled in the control deck.
- If the solution has stale Visual Studio cache issues, close Visual Studio and delete the `.vs` folder once before reopening the solution.
