using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchMark.App.Models;
using WatchMark.App.Services;
using WinForms = System.Windows.Forms;

namespace WatchMark.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly LibraryScannerService _libraryScannerService;
    private readonly WatchStatusRepository _watchStatusRepository;
    private readonly VlcLauncherService _vlcLauncherService;
    private readonly VlcHttpMonitorService _vlcHttpMonitor;
    private readonly AppSettings _settings;
    private readonly string _appsettingsPath;
    private MovieItem? _trackedMovie;
    private double _lastSavedProgress;
    private bool _isInitializing = true;
    private const int VlcHttpPort = 8080;
    private const string VlcHttpPassword = "vlchttp";
    private const string LastOpenPlaylistName = "Last Open";
    private const string VlcTitlePrefix = "vlc-title::";

    public ObservableCollection<MovieItem> Movies { get; } = [];
    public ObservableCollection<MovieItem> FilteredMovies { get; } = [];
    public ObservableCollection<string> RecentLibraryPaths { get; } = [];
    public ObservableCollection<Playlist> Playlists { get; } = [];

    public ICommand ScanCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand MarkSelectedWatchedCommand { get; }
    public ICommand MarkSelectedUnwatchedCommand { get; }
    public ICommand AddPlaylistCommand { get; }
    public ICommand DeletePlaylistCommand { get; }

    [ObservableProperty]
    private MovieItem? selectedMovie;

    [ObservableProperty]
    private Playlist? selectedPlaylist;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string selectedLibraryPath = string.Empty;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string currentFolderName = string.Empty;

    [ObservableProperty]
    private bool isVlcHttpConnected;

    [ObservableProperty]
    private string vlcStatusText = "VLC: Checking...";

    public MainViewModel()
    {
        _settingsService = new SettingsService();

        var appDirectory = AppContext.BaseDirectory;
        var defaultAppsettingsPath = Path.Combine(appDirectory, "appsettings.json");
        var userSettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WatchMark");
        _appsettingsPath = Path.Combine(userSettingsDirectory, "appsettings.json");

        _settings = File.Exists(_appsettingsPath)
            ? _settingsService.Load(_appsettingsPath)
            : _settingsService.Load(defaultAppsettingsPath);

        if (!Path.IsPathRooted(_settings.DatabasePath))
        {
            _settings.DatabasePath = Path.Combine(appDirectory, _settings.DatabasePath);
        }

        _libraryScannerService = new LibraryScannerService();
        _watchStatusRepository = new WatchStatusRepository(_settings.DatabasePath);
        _vlcLauncherService = new VlcLauncherService(_settings.VlcPath);
        _vlcHttpMonitor = new VlcHttpMonitorService(VlcHttpPort, VlcHttpPassword);
        _vlcHttpMonitor.StatusChanged += OnVlcStatusChanged;
        _vlcHttpMonitor.NewFileDetected += OnNewFileDetected;
        _vlcHttpMonitor.HttpConnectionChanged += OnVlcHttpConnectionChanged;

        // Populate recent paths first
        Debug.WriteLine($"INIT: _settings.RecentLibraryPaths count = {_settings.RecentLibraryPaths?.Count ?? 0}");
        if (_settings.RecentLibraryPaths is not null)
        {
            foreach (var path in _settings.RecentLibraryPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (!RecentLibraryPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"INIT: Adding recent path: {path}");
                    RecentLibraryPaths.Add(path);
                }
            }
        }
        
        Debug.WriteLine($"INIT: RecentLibraryPaths now has {RecentLibraryPaths.Count} items");
        
        // Ensure the current library path is in the recent list
        var libraryPath = _settings.LibraryPath ?? string.Empty;
        Debug.WriteLine($"INIT: libraryPath from settings = '{libraryPath}'");
        if (!string.IsNullOrWhiteSpace(libraryPath) && !RecentLibraryPaths.Contains(libraryPath, StringComparer.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"INIT: Adding current library path to recent: {libraryPath}");
            RecentLibraryPaths.Insert(0, libraryPath);
        }
        
        Debug.WriteLine($"INIT: Final RecentLibraryPaths count = {RecentLibraryPaths.Count}");
        
        // Now set the selected path (after it's been added to the collection)
        SelectedLibraryPath = libraryPath;
        
        // Update folder name display
        if (!string.IsNullOrWhiteSpace(libraryPath))
        {
            var folderName = Path.GetFileName(libraryPath.TrimEnd('\\', '/'));
            CurrentFolderName = string.IsNullOrWhiteSpace(folderName) ? libraryPath : folderName;
            Debug.WriteLine($"INIT: CurrentFolderName set to: {CurrentFolderName}");
        }

        PersistSettings();

        ScanCommand = new RelayCommand(ScanLibrary);
        BrowseCommand = new RelayCommand(BrowseForLibraryPath);
        MarkSelectedWatchedCommand = new RelayCommand(MarkSelectedWatched, () => SelectedMovie is not null);
        MarkSelectedUnwatchedCommand = new RelayCommand(MarkSelectedUnwatched, () => SelectedMovie is not null);
        AddPlaylistCommand = new RelayCommand(AddNewPlaylist);
        DeletePlaylistCommand = new RelayCommand<Playlist>(DeletePlaylist);
        
        // Mark initialization as complete before creating playlists
        _isInitializing = false;

        // Fixed playlist for files opened directly in VLC (e.g., from Windows Explorer)
        var lastOpenPlaylist = new Playlist(LastOpenPlaylistName, string.Empty, true);
        Playlists.Add(lastOpenPlaylist);
        
        // Load previously detected Last Open entries from database
        LoadLastOpenEntries(lastOpenPlaylist);
        
        // Initialize default playlist
        if (!string.IsNullOrWhiteSpace(libraryPath) && Directory.Exists(libraryPath))
        {
            var defaultPlaylist = new Playlist("Main Library", libraryPath);
            Playlists.Add(defaultPlaylist);
            SelectedPlaylist = defaultPlaylist; // This will trigger scan via OnSelectedPlaylistChanged
        }
        else
        {
            SelectedPlaylist = lastOpenPlaylist;
            StatusText = "Play a movie in VLC to populate Last Open, or click '+ New Playlist'";
        }
        
        // Start background VLC monitoring to auto-detect playback
        Debug.WriteLine("INIT: Starting continuous VLC monitoring");
        _vlcHttpMonitor.StartMonitoring(); // No file path = continuous monitoring
        StatusText = "Background monitoring active - play movies in VLC to track them";
    }

    private void BrowseForLibraryPath()
    {
        try
        {
            Debug.WriteLine("BrowseForLibraryPath: Dialog opening...");
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select Movie Library Folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrWhiteSpace(SelectedLibraryPath) && Directory.Exists(SelectedLibraryPath))
            {
                dialog.SelectedPath = SelectedLibraryPath;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                Debug.WriteLine($"BrowseForLibraryPath: Selected path = {dialog.SelectedPath}");
                SelectedLibraryPath = dialog.SelectedPath;
                // NOTE: Setting SelectedLibraryPath triggers OnSelectedLibraryPathChanged partial method
                // which already calls ScanLibrary(), so we DON'T call it again here
                Debug.WriteLine($"BrowseForLibraryPath: SelectedLibraryPath set to {SelectedLibraryPath}");
                AddRecentPath(SelectedLibraryPath);
                _settings.LibraryPath = SelectedLibraryPath;
                PersistSettings();
            }
            else
            {
                Debug.WriteLine("BrowseForLibraryPath: Dialog cancelled");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BrowseForLibraryPath Exception: {ex}");
            StatusText = $"Error: {ex.Message}";
            System.Windows.MessageBox.Show($"Browse Error: {ex.Message}\n\n{ex.StackTrace}", "WatchMark", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ScanLibrary()
    {
        Debug.WriteLine("ScanLibrary: Starting scan");
        try
        {
            foreach (var existingMovie in Movies)
            {
                existingMovie.PropertyChanged -= OnMoviePropertyChanged;
            }

            Movies.Clear();

            var libraryPath = (SelectedLibraryPath ?? string.Empty).Trim();
            Debug.WriteLine($"ScanLibrary: SelectedLibraryPath property = '{SelectedLibraryPath}'");
            Debug.WriteLine($"ScanLibrary: libraryPath after trim = '{libraryPath}'");
            
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                StatusText = "Library path is empty.";
                Debug.WriteLine("ScanLibrary: Path is empty, returning");
                return;
            }

            if (!Directory.Exists(libraryPath))
            {
                StatusText = $"Library path not found: {libraryPath}";
                Debug.WriteLine($"ScanLibrary: Directory does not exist: {libraryPath}");
                return;
            }

            Debug.WriteLine($"ScanLibrary: Path is valid, proceeding with scan");

            _settings.LibraryPath = libraryPath;
            PersistSettings();
            Debug.WriteLine($"ScanLibrary: About to scan directory: {libraryPath}");

            var scannedMovies = _libraryScannerService.Scan(libraryPath).ToList();
            Debug.WriteLine($"ScanLibrary: Scanner returned {scannedMovies.Count} movie(s)");

            var persisted = _watchStatusRepository.LoadAll();
            foreach (var movie in scannedMovies)
            {
                Debug.WriteLine($"ScanLibrary: Adding movie: {movie.Title}");
                if (persisted.TryGetValue(movie.FilePath, out var state))
                {
                    movie.ProgressPercent = state.ProgressPercent;
                    movie.IsWatched = state.IsWatched;
                    movie.LastWatchedUtc = state.LastWatchedUtc;
                    movie.Duration = TimeSpan.FromSeconds(state.Duration);
                    movie.TimeSeconds = state.TimeSeconds;
                }

                movie.PropertyChanged += OnMoviePropertyChanged;
                Movies.Add(movie);
            }

            StatusText = $"Scanned {Movies.Count} movie(s) from {libraryPath}";
            Debug.WriteLine($"ScanLibrary: Scan complete. Total movies in UI: {Movies.Count}");
            
            // Update folder name display
            CurrentFolderName = Path.GetFileName(libraryPath.TrimEnd('\\', '/'));
            if (string.IsNullOrWhiteSpace(CurrentFolderName))
            {
                CurrentFolderName = libraryPath; // Root path like C:\
            }
            
            // Update filtered movies
            ApplyFilter();

            // Keep selected playlist cache in sync for normal (folder-based) playlists
            if (SelectedPlaylist is not null && !SelectedPlaylist.IsFixed)
            {
                SelectedPlaylist.Movies.Clear();
                foreach (var movie in Movies)
                {
                    SelectedPlaylist.Movies.Add(movie);
                }
            }
            
            // If no movies found, show debug info about what files exist in the directory
            if (Movies.Count == 0)
            {
                try
                {
                    var allFiles = Directory.GetFiles(libraryPath, "*", SearchOption.AllDirectories);
                    Debug.WriteLine($"ScanLibrary DEBUG: Total files in directory tree: {allFiles.Length}");
                    if (allFiles.Length > 0)
                    {
                        Debug.WriteLine($"ScanLibrary DEBUG: First 10 files found:");
                        foreach (var file in allFiles.Take(10))
                        {
                            var ext = Path.GetExtension(file).ToLower();
                            Debug.WriteLine($"  - {Path.GetFileName(file)} (ext: {ext})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ScanLibrary DEBUG: Could not list files: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScanLibrary Exception: {ex}");
            StatusText = $"Scan error: {ex.Message}";
        }
    }

    private void MarkSelectedWatched()
    {
        if (SelectedMovie is null)
        {
            return;
        }

        SetMovieWatchedState(SelectedMovie, true);
        StatusText = $"Marked watched: {SelectedMovie.Title}";
    }

    private void MarkSelectedUnwatched()
    {
        if (SelectedMovie is null)
        {
            return;
        }

        SetMovieWatchedState(SelectedMovie, false);
        StatusText = $"Marked unwatched: {SelectedMovie.Title}";
    }

    public void OpenSelectedMovieInVlc()
    {
        OpenMovieInVlc(SelectedMovie);
    }

    public void OpenMovieInVlc(MovieItem? movie)
    {
        if (movie is null)
        {
            StatusText = "Select a movie first.";
            return;
        }

        // For title-only entries, can't open file
        if (movie.FilePath.StartsWith("vlc-title://", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Cannot open title-only entry - file path unknown";
            return;
        }

        var startTime = movie.TimeSeconds > 0 && movie.ProgressPercent < 95 ? movie.TimeSeconds : 0;

        if (_vlcLauncherService.TryOpen(movie.FilePath, out var errorMessage, VlcHttpPort, VlcHttpPassword, startTime))
        {
            _trackedMovie = movie;
            _lastSavedProgress = movie.ProgressPercent;
            movie.LastWatchedUtc = DateTimeOffset.UtcNow;
            _watchStatusRepository.Save(movie);
            _vlcHttpMonitor.StartMonitoring(movie.FilePath);
            
            var resumeMsg = startTime > 0 ? $" (resuming from {TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss})" : "";
            StatusText = $"Opened in VLC: {movie.Title}{resumeMsg}";
            return;
        }

        StatusText = errorMessage;
    }

    public void AddRecentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existing = RecentLibraryPaths.FirstOrDefault(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentLibraryPaths.Remove(existing);
        }

        RecentLibraryPaths.Insert(0, path);

        while (RecentLibraryPaths.Count > 5)
        {
            RecentLibraryPaths.RemoveAt(RecentLibraryPaths.Count - 1);
        }

        _settings.RecentLibraryPaths = RecentLibraryPaths.ToList();
    }

    private void PersistSettings()
    {
        _settingsService.Save(_appsettingsPath, _settings);
    }

    private void OnVlcStatusChanged(object? sender, PlaybackStatus status)
    {
        void Update()
        {
            var trackedMovie = _trackedMovie;
            if (trackedMovie is null || status is null)
            {
                return;
            }

            // Update the tracked movie
            trackedMovie.ProgressPercent = status.ProgressPercent;
            trackedMovie.TimeSeconds = status.TimeSeconds;
            
            if (status.LengthSeconds > 0)
            {                trackedMovie.Duration = TimeSpan.FromSeconds(status.LengthSeconds);
            }
            
            if (status.ProgressPercent >= _settings.WatchedThresholdPercent)
            {
                trackedMovie.IsWatched = true;
            }

            // Make sure the movie in the Movies collection is also updated
            var movieInCollection = Movies.FirstOrDefault(m => string.Equals(m.FilePath, trackedMovie.FilePath, StringComparison.OrdinalIgnoreCase));
            if (movieInCollection is not null)
            {
                movieInCollection.ProgressPercent = trackedMovie.ProgressPercent;
                movieInCollection.IsWatched = trackedMovie.IsWatched;
                movieInCollection.LastWatchedUtc = trackedMovie.LastWatchedUtc;
                movieInCollection.TimeSeconds = trackedMovie.TimeSeconds;
                if (status.LengthSeconds > 0)
                {
                    movieInCollection.Duration = TimeSpan.FromSeconds(status.LengthSeconds);
                }
            }

            // Also update in FilteredMovies if present
            var movieInFiltered = FilteredMovies.FirstOrDefault(m => string.Equals(m.FilePath, trackedMovie.FilePath, StringComparison.OrdinalIgnoreCase));
            if (movieInFiltered is not null)
            {
                movieInFiltered.ProgressPercent = trackedMovie.ProgressPercent;
                movieInFiltered.IsWatched = trackedMovie.IsWatched;
                movieInFiltered.LastWatchedUtc = trackedMovie.LastWatchedUtc;
                movieInFiltered.TimeSeconds = trackedMovie.TimeSeconds;
                if (status.LengthSeconds > 0)
                {
                    movieInFiltered.Duration = TimeSpan.FromSeconds(status.LengthSeconds);
                }
            }

            if (status.HasEnded)
            {
                trackedMovie.ProgressPercent = 100;
                trackedMovie.IsWatched = true;
                if (movieInCollection is not null)
                {
                    movieInCollection.ProgressPercent = 100;
                    movieInCollection.IsWatched = true;
                }
                if (movieInFiltered is not null)
                {
                    movieInFiltered.ProgressPercent = 100;
                    movieInFiltered.IsWatched = true;
                }
                _vlcHttpMonitor.StopMonitoring();
                StatusText = $"Playback ended: {trackedMovie.Title}";
            }

            if (Math.Abs(status.ProgressPercent - _lastSavedProgress) >= 5 || trackedMovie.IsWatched)
            {
                _watchStatusRepository.Save(trackedMovie);
                _lastSavedProgress = status.ProgressPercent;
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Update);
            return;
        }

        Update();
    }

    partial void OnSelectedLibraryPathChanged(string value)
    {
        Debug.WriteLine($"OnSelectedLibraryPathChanged called with value: '{value}'");
        Debug.WriteLine($"OnSelectedLibraryPathChanged: _isInitializing = {_isInitializing}");
        
        // Update folder name display
        if (!string.IsNullOrWhiteSpace(value))
        {
            CurrentFolderName = Path.GetFileName(value.TrimEnd('\\', '/'));
            if (string.IsNullOrWhiteSpace(CurrentFolderName))
            {
                CurrentFolderName = value;
            }
        }
        else
        {
            var fallbackPath = _settings.LibraryPath;
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                CurrentFolderName = Path.GetFileName(fallbackPath.TrimEnd('\\', '/'));
                if (string.IsNullOrWhiteSpace(CurrentFolderName))
                {
                    CurrentFolderName = fallbackPath;
                }
            }
            else if (Movies.Count == 0)
            {
                CurrentFolderName = string.Empty;
            }
        }
        
        // Only auto-scan after initialization when user actively changes the path
        if (!_isInitializing && !string.IsNullOrWhiteSpace(value))
        {
            Debug.WriteLine($"OnSelectedLibraryPathChanged: Proceeding with scan for path: {value}");
            AddRecentPath(value);
            _settings.LibraryPath = value;
            PersistSettings();
            ScanLibrary(); // Let ScanLibrary handle validation
        }
        else
        {
            Debug.WriteLine($"OnSelectedLibraryPathChanged: Skipping - initializing={_isInitializing}, empty={string.IsNullOrWhiteSpace(value)}");
        }
    }

    partial void OnSelectedMovieChanged(MovieItem? value)
    {
        (MarkSelectedWatchedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MarkSelectedUnwatchedCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.IsFixed)
        {
            SelectedLibraryPath = string.Empty;
            CurrentFolderName = value.Name;
            Movies.Clear();
            foreach (var movie in value.Movies)
            {
                Movies.Add(movie);
            }
            ApplyFilter();
            StatusText = value.Movies.Count == 0
                ? "Last Open is empty - open a movie in VLC from Explorer"
                : $"Showing {value.Movies.Count} item(s) from Last Open";
            return;
        }

        if (!string.IsNullOrWhiteSpace(value.LibraryPath))
        {
            // Switch to selected playlist's library
            SelectedLibraryPath = value.LibraryPath;
            CurrentFolderName = value.Name;
            
            // Scan the library to populate movies
            ScanLibrary();
        }
    }

    private void OnNewFileDetected(object? sender, string filePath)
    {
        void Update()
        {
            Debug.WriteLine($"NEW FILE DETECTED: {filePath}");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var isTitleOnlyDetection = filePath.StartsWith(VlcTitlePrefix, StringComparison.OrdinalIgnoreCase);
            var titleOnlyText = isTitleOnlyDetection
                ? filePath.Substring(VlcTitlePrefix.Length).Trim()
                : string.Empty;

            // Filter out generic VLC titles
            if (isTitleOnlyDetection && 
                (string.Equals(titleOnlyText, "vlc", StringComparison.OrdinalIgnoreCase) ||
                 titleOnlyText.Length < 3))
            {
                return;
            }

            if (!isTitleOnlyDetection && !File.Exists(filePath))
            {
                return;
            }

            // Check if movie already exists in current playlist/library
            var existingMovie = Movies.FirstOrDefault(m => 
                string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            
            if (existingMovie is not null)
            {
                Debug.WriteLine($"Movie already tracked: {existingMovie.Title}");
                _trackedMovie = existingMovie;
                _lastSavedProgress = existingMovie.ProgressPercent;
                StatusText = $"Playing: {existingMovie.Title}";
                return;
            }

            MovieItem? newMovie;

            if (isTitleOnlyDetection)
            {
                if (string.IsNullOrWhiteSpace(titleOnlyText))
                {
                    return;
                }

                var syntheticPath = $"vlc-title://{titleOnlyText}";
                newMovie = new MovieItem
                {
                    Title = titleOnlyText,
                    FilePath = syntheticPath,
                    LastWatchedUtc = DateTimeOffset.UtcNow
                };
            }
            else
            {
                // Auto-index new movie
                Debug.WriteLine($"Auto-indexing new movie: {filePath}");
                var directory = Path.GetDirectoryName(filePath);

                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                // Scan just this one file
                var scannedMovies = _libraryScannerService.Scan(directory).Where(m =>
                    string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();

                if (scannedMovies.Count == 0)
                {
                    Debug.WriteLine($"File not recognized as movie: {filePath}");
                    return;
                }

                newMovie = scannedMovies[0];

                // Load watch status from database
                var persisted = _watchStatusRepository.LoadAll();
                if (persisted.TryGetValue(newMovie.FilePath, out var state))
                {
                    newMovie.ProgressPercent = state.ProgressPercent;
                    newMovie.IsWatched = state.IsWatched;
                    newMovie.LastWatchedUtc = state.LastWatchedUtc;
                    newMovie.Duration = TimeSpan.FromSeconds(state.Duration);
                    newMovie.TimeSeconds = state.TimeSeconds;
                }
            }

            // Only add real file paths to Last Open (not title-only entries)
            if (!isTitleOnlyDetection)
            {
                var lastOpenPlaylist = Playlists.FirstOrDefault(p => p.IsFixed &&
                    string.Equals(p.Name, LastOpenPlaylistName, StringComparison.OrdinalIgnoreCase));

                if (lastOpenPlaylist is not null)
                {
                    var existingInLastOpen = lastOpenPlaylist.Movies.FirstOrDefault(m =>
                        string.Equals(m.FilePath, newMovie.FilePath, StringComparison.OrdinalIgnoreCase));

                    if (existingInLastOpen is not null)
                    {
                        newMovie = existingInLastOpen;
                        newMovie.LastWatchedUtc = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        lastOpenPlaylist.Movies.Insert(0, newMovie);
                    }
                }
            }

            newMovie.PropertyChanged -= OnMoviePropertyChanged;
            newMovie.PropertyChanged += OnMoviePropertyChanged;

            if (SelectedPlaylist?.IsFixed == true)
            {
                Movies.Clear();
                foreach (var movie in SelectedPlaylist.Movies)
                {
                    Movies.Add(movie);
                }
                ApplyFilter();
            }
            else if (!Movies.Any(m => string.Equals(m.FilePath, newMovie.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                Movies.Add(newMovie);
                ApplyFilter();
            }
            
            _trackedMovie = newMovie;
            _lastSavedProgress = newMovie.ProgressPercent;
            
            // Save to database so it persists across restarts
            _watchStatusRepository.Save(newMovie);
            
            StatusText = $"Auto-indexed and tracking: {newMovie.Title}";
            Debug.WriteLine($"Auto-indexed: {newMovie.Title}");
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Update);
            return;
        }

        Update();
    }
    
    private void LoadLastOpenEntries(Playlist lastOpenPlaylist)
    {
        try
        {
            var persisted = _watchStatusRepository.LoadAll();
            
            // Load entries that have been watched (exclude vlc-title:// since they can't be opened)
            var lastOpenEntries = persisted
                .Where(kvp => !kvp.Key.StartsWith("vlc-title://", StringComparison.OrdinalIgnoreCase) &&
                             kvp.Value.LastWatchedUtc.HasValue)
                .OrderByDescending(kvp => kvp.Value.LastWatchedUtc ?? DateTimeOffset.MinValue)
                .Take(50) // Limit to last 50 entries
                .ToList();
                
            foreach (var entry in lastOpenEntries)
            {
                var movie = new MovieItem
                {
                    FilePath = entry.Key,
                    Title = entry.Key.StartsWith("vlc-title://", StringComparison.OrdinalIgnoreCase)
                        ? entry.Key.Substring("vlc-title://".Length)
                        : Path.GetFileNameWithoutExtension(entry.Key),
                    ProgressPercent = entry.Value.ProgressPercent,
                    IsWatched = entry.Value.IsWatched,
                    LastWatchedUtc = entry.Value.LastWatchedUtc,
                    Duration = TimeSpan.FromSeconds(entry.Value.Duration),
                    TimeSeconds = entry.Value.TimeSeconds
                };
                
                movie.PropertyChanged -= OnMoviePropertyChanged;
                movie.PropertyChanged += OnMoviePropertyChanged;
                lastOpenPlaylist.Movies.Add(movie);
            }
            
            Debug.WriteLine($"Loaded {lastOpenEntries.Count} Last Open entries from database");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading Last Open entries: {ex.Message}");
        }
    }

    private void OnVlcHttpConnectionChanged(object? sender, bool isConnected)
    {
        void Update()
        {
            IsVlcHttpConnected = isConnected;
            VlcStatusText = isConnected 
                ? "VLC HTTP: Connected ✓" 
                : "VLC HTTP: Disconnected (title-only detection)";
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Update);
            return;
        }

        Update();
    }

    private void AddNewPlaylist()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select Playlist Library Folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            var path = dialog.SelectedPath;
            var name = Path.GetFileName(path.TrimEnd('\\', '/'));
            
            if (string.IsNullOrWhiteSpace(name))
            {
                name = path;
            }

            var playlist = new Playlist(name, path);
            Playlists.Add(playlist);
            SelectedPlaylist = playlist;
            
            AddRecentPath(path);
            PersistSettings();
        }
    }

    private void DeletePlaylist(Playlist? playlist)
    {
        if (playlist is null)
        {
            return;
        }

        if (playlist.IsFixed)
        {
            StatusText = $"{playlist.Name} is a fixed playlist and cannot be deleted";
            return;
        }

        if (Playlists.Count <= 1)
        {
            // Don't allow deleting the last playlist
            StatusText = "Cannot delete the last playlist";
            return;
        }

        var index = Playlists.IndexOf(playlist);
        Playlists.Remove(playlist);
        
        // Select another playlist after deletion
        if (Playlists.Count > 0)
        {
            var newIndex = Math.Min(index, Playlists.Count - 1);
            SelectedPlaylist = Playlists[newIndex];
        }
        
        StatusText = $"Deleted playlist: {playlist.Name}";
    }

    private void ApplyFilter()
    {
        FilteredMovies.Clear();
        
        var filter = FilterText?.Trim().ToLowerInvariant() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(filter))
        {
            // No filter, show all movies
            foreach (var movie in Movies)
            {
                FilteredMovies.Add(movie);
            }
        }
        else
        {
            // Filter by title (case-insensitive)
            foreach (var movie in Movies)
            {
                if (movie.Title.ToLowerInvariant().Contains(filter))
                {
                    FilteredMovies.Add(movie);
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var movie in Movies)
        {
            movie.PropertyChanged -= OnMoviePropertyChanged;
        }

        _vlcHttpMonitor.StatusChanged -= OnVlcStatusChanged;
        _vlcHttpMonitor.NewFileDetected -= OnNewFileDetected;
        _vlcHttpMonitor.HttpConnectionChanged -= OnVlcHttpConnectionChanged;
        _vlcHttpMonitor.Dispose();
    }

    private void OnMoviePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MovieItem movie)
        {
            return;
        }

        if (e.PropertyName == nameof(MovieItem.IsWatched))
        {
            if (movie.IsWatched)
            {
                if (movie.ProgressPercent < 100)
                {
                    movie.ProgressPercent = 100;
                }

                movie.LastWatchedUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                movie.ProgressPercent = 0;
                movie.LastWatchedUtc = null;
            }

            _watchStatusRepository.Save(movie);
        }
    }

    private void SetMovieWatchedState(MovieItem movie, bool isWatched)
    {
        movie.IsWatched = isWatched;
        if (isWatched)
        {
            movie.ProgressPercent = 100;
            movie.LastWatchedUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            movie.ProgressPercent = 0;
            movie.LastWatchedUtc = null;
        }

        _watchStatusRepository.Save(movie);
    }
}
