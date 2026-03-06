# Architecture

## App layers
- Models: movie records and settings.
- Services: settings load, library scan, VLC progress tracking, SQLite persistence.
- ViewModels: command orchestration and UI-bound movie state.

## Watched logic
- Mark watched when playback progress reaches configured threshold.
- Persist status by full file path.

## Future integration
- Auto-detect external VLC sessions and map to library items.
- Add playback resume position and per-profile state.
