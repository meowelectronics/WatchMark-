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

    public ObservableCollection<MovieItem> Movies { get; } = [];
    public ObservableCollection<MovieItem> FilteredMovies { get; } = [];
    public ObservableCollection<string> RecentLibraryPaths { get; } = [];

    public ICommand ScanCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand MarkSelectedWatchedCommand { get; }
    public ICommand MarkSelectedUnwatchedCommand { get; }

    [ObservableProperty]
    private MovieItem? selectedMovie;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string selectedLibraryPath = string.Empty;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string currentFolderName = string.Empty;

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
        
        // Initial scan on startup if path exists
        _isInitializing = false;
        Debug.WriteLine($"INIT: SelectedLibraryPath = '{SelectedLibraryPath}'");
        Debug.WriteLine($"INIT: _settings.LibraryPath = '{_settings.LibraryPath}'");
        Debug.WriteLine($"INIT: RecentLibraryPaths.Count = {RecentLibraryPaths.Count}");
        if (!string.IsNullOrWhiteSpace(_settings.LibraryPath) && Directory.Exists(_settings.LibraryPath))
        {
            Debug.WriteLine("INIT: Valid path found, starting scan");
            ScanLibrary();
        }
        else
        {
            Debug.WriteLine("INIT: No valid path, showing prompt");
            StatusText = "Select a library folder to begin tracking movies";
        }
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

        if (_vlcLauncherService.TryOpen(movie.FilePath, out var errorMessage, VlcHttpPort, VlcHttpPassword))
        {
            _trackedMovie = movie;
            _lastSavedProgress = movie.ProgressPercent;
            movie.LastWatchedUtc = DateTimeOffset.UtcNow;
            _watchStatusRepository.Save(movie);
            _vlcHttpMonitor.StartMonitoring(movie.FilePath);
            StatusText = $"Opened in VLC: {movie.Title}";
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
            }

            // Also update in FilteredMovies if present
            var movieInFiltered = FilteredMovies.FirstOrDefault(m => string.Equals(m.FilePath, trackedMovie.FilePath, StringComparison.OrdinalIgnoreCase));
            if (movieInFiltered is not null)
            {
                movieInFiltered.ProgressPercent = trackedMovie.ProgressPercent;
                movieInFiltered.IsWatched = trackedMovie.IsWatched;
                movieInFiltered.LastWatchedUtc = trackedMovie.LastWatchedUtc;
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
