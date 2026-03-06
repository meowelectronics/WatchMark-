# MovieWatchTracker

Local-first Windows WPF app for tracking watched/unwatched movies with VLC playback events and SQLite persistence.

## Current setup
- WPF app: `src/MovieWatchTracker.App`
- Local DB: `data/watchstatus.db`
- Config: `src/MovieWatchTracker.App/appsettings.json`

## Prerequisites
- .NET 8 SDK (required to build/run)
- VLC installed on Windows (for LibVLC runtime)

## Build
```powershell
cd .
dotnet restore .\MovieWatchTracker.sln
dotnet build .\MovieWatchTracker.sln
```

## Notes
- Watched threshold defaults to 90%.
- `LibraryPath` should be updated to your actual movie folder.
- No remote push/commit is performed by setup.
