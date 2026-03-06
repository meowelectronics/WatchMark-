# 🎬 WatchMark

> **Smart Movie Progress Tracker with VLC Integration**

WatchMark is a modern Windows desktop application that automatically tracks your movie watching progress by integrating seamlessly with VLC Media Player. Never lose your place in a movie again—WatchMark remembers exactly where you stopped and lets you resume with a single click.

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](https://github.com/meowelectronics/WatchMark-/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## ✨ Key Features

### 🎯 **Automatic VLC Detection**
- Real-time monitoring of VLC playback using HTTP API
- Automatic detection of movies you're watching
- No manual entry required—just play your movie in VLC

### ⏱️ **Resume Playback**
- Double-click any movie to resume from exactly where you stopped
- Automatically skips movies watched over 95%
- Precise timestamp tracking (HH:MM:SS)

### 📊 **Progress Tracking**
- Visual progress percentage for each movie
- "Stopped At" column showing exact playback position
- Duration information for all tracked movies
- Mark movies as watched with a simple checkbox

### 📚 **Smart Organization**
- **Last Open** playlist: Shows your recently watched movies
- **Custom Playlists**: Create and organize your movie libraries
- Search and filter functionality
- Clean, modern interface with contemporary styling

### 💾 **Local-First Design**
- All data stored locally in SQLite database
- Privacy-focused—no cloud dependencies
- Fast, responsive performance
- Persistent across app restarts

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (64-bit)
- **VLC Media Player** ([Download](https://www.videolan.org/vlc/))
- **.NET 8 Runtime** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))

### Installation

1. **Download the latest release** from [Releases](https://github.com/meowelectronics/WatchMark-/releases)
2. **Extract** the ZIP file to your desired location
3. **Run** `WatchMark.exe`

### First-Time Setup

On first launch, WatchMark will guide you to:

1. **Enable VLC HTTP Interface:**
   - Open VLC → Tools → Preferences → Show All
   - Navigate to Interface → Main interfaces
   - Check "Web" (HTTP interface)
   - Under Interface → Main interfaces → Lua, set password to: `vlchttp`
   - Restart VLC

2. **Add Your Movie Library:**
   - Click "+ New Playlist"
   - Browse to your movie folder
   - Movies will be automatically scanned

---

## 📖 Usage Guide

### Playing & Tracking Movies

1. **From WatchMark:**
   - Double-click any movie in your library
   - WatchMark opens it in VLC and starts tracking progress

2. **From Windows Explorer:**
   - Right-click any video file → Open with VLC
   - WatchMark automatically detects and tracks it

3. **Resume Watching:**
   - Double-click a partially watched movie
   - VLC automatically jumps to your last position

### Managing Your Library

- **Add Playlist:** Click "+ New Playlist" button
- **Delete Playlist:** Click the trash icon next to playlist name (except "Last Open")
- **Search Movies:** Use the search box to filter by title
- **Filter Status:** Toggle between "Watched" and "Unwatched" views
- **Mark as Watched:** Check the box in the "Watched" column

### Understanding the Interface

| Column | Description |
|--------|-------------|
| ✓ | Visual checkmark indicator |
| **Title** | Movie filename |
| **Duration** | Total runtime |
| **Progress** | Percentage watched |
| **Stopped At** | Last playback position (HH:MM:SS) |
| **Watched** | Manual toggle |
| **Last Watched** | Last viewing timestamp |

---

## 🛠️ Building from Source

### Development Prerequisites

- **.NET 8 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Visual Studio 2022** or **VS Code** with C# extension
- **Git**

### Clone and Build

```powershell
# Clone repository
git clone https://github.com/meowelectronics/WatchMark-.git
cd WatchMark-

# Restore dependencies
dotnet restore .\WatchMark.sln

# Build (Debug)
dotnet build .\WatchMark.sln

# Build (Release)
dotnet build .\WatchMark.sln --configuration Release

# Run
.\src\WatchMark.App\bin\Debug\net8.0-windows\win-x64\WatchMark.exe
```

### Run Tests

```powershell
dotnet test .\WatchMark.sln
```

All 105 unit tests covering security, services, and models should pass.

---

## 🏗️ Architecture

### Technology Stack

- **Framework:** .NET 8.0 (Windows)
- **UI:** WPF (Windows Presentation Foundation)
- **MVVM:** CommunityToolkit.Mvvm
- **Database:** SQLite (Microsoft.Data.Sqlite)
- **Media:** LibVLCSharp
- **Testing:** xUnit

### Project Structure

```
WatchMark/
├── src/
│   ├── WatchMark.App/              # Main WPF application
│   │   ├── Models/                 # Data models
│   │   ├── ViewModels/             # MVVM view models
│   │   ├── Services/               # Business logic
│   │   │   ├── VlcHttpMonitorService.cs
│   │   │   ├── VlcLauncherService.cs
│   │   │   ├── LibraryScannerService.cs
│   │   │   └── WatchStatusRepository.cs
│   │   └── Views/                  # XAML UI
│   └── WatchMark.Tests/            # Unit tests
├── data/                           # SQLite database
└── docs/                           # Documentation
```

### Key Features Implementation

- **VLC Integration:** Dual-mode detection (HTTP API + window title fallback)
- **Progress Tracking:** Real-time polling every 2 seconds
- **Resume Playback:** VLC `--start-time` parameter
- **Database Schema:** Automatic migration for Duration/TimeSeconds columns

---

## 🔧 Configuration

### Settings File

Located at: `%LOCALAPPDATA%\WatchMark\appsettings.json`

```json
{
  "LibraryPath": "C:\\Movies",
  "WatchedThreshold": 90.0,
  "VlcPath": "C:\\Program Files\\VideoLAN\\VLC\\vlc.exe",
  "HttpPort": 8080,
  "HttpPassword": "vlchttp"
}
```

### Database Location

SQLite database: `%LOCALAPPDATA%\WatchMark\watchstatus.db`

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **VLC Media Player** - Powerful open-source media player
- **LibVLCSharp** - .NET bindings for LibVLC
- **Community Toolkit** - Modern MVVM framework

---

## 📧 Support

Having issues? Please check the [Issues](https://github.com/meowelectronics/WatchMark-/issues) page or create a new issue.

---

<div align="center">
Made with ❤️ by <a href="https://github.com/meowelectronics">meowelectronics</a>
</div>
