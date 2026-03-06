using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.IO;
using System.Diagnostics;

namespace WatchMark.App.Services;

public class VlcHttpMonitorService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly System.Timers.Timer _pollTimer;
    private readonly string _password;
    private readonly int _port;
    private string? _currentFilePath;
    private bool _continuousMonitoring;
    private string? _currentWindowTitle;
    private bool _lastHttpConnected;
    private const string TitlePrefix = "vlc-title::";

    public event EventHandler<PlaybackStatus>? StatusChanged;
    public event EventHandler<string>? NewFileDetected;
    public event EventHandler<bool>? HttpConnectionChanged;

    public VlcHttpMonitorService(int port = 8080, string password = "vlchttp")
    {
        _port = port;
        _password = password;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var authBytes = Encoding.ASCII.GetBytes($":{_password}");
        var authHeader = Convert.ToBase64String(authBytes);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

        _pollTimer = new System.Timers.Timer(2000); // Poll every 2 seconds
        _pollTimer.AutoReset = false; // Prevent overlapping requests
        _pollTimer.Elapsed += OnPollTimerElapsed;
    }

    public void StartMonitoring(string? filePath = null)
    {
        _currentFilePath = filePath;
        _continuousMonitoring = filePath is null; // Continuous if no specific file
        if (!_pollTimer.Enabled)
        {
            _pollTimer.Start();
        }
    }

    public void StopMonitoring()
    {
        _pollTimer.Stop();
        _currentFilePath = null;
        _continuousMonitoring = false;
    }

    private async void OnPollTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var status = await GetPlaybackStatusAsync();
            if (status is not null)
            {
                if (!_lastHttpConnected)
                {
                    _lastHttpConnected = true;
                    HttpConnectionChanged?.Invoke(this, true);
                }
                StatusChanged?.Invoke(this, status);
            }
        }
        catch (Exception ex)
        {
            // VLC might not be running or HTTP interface not available
            System.Diagnostics.Debug.WriteLine($"VLC polling error: {ex.Message}");

            if (_lastHttpConnected)
            {
                _lastHttpConnected = false;
                HttpConnectionChanged?.Invoke(this, false);
            }

            // Fallback: detect currently playing media from VLC window title
            TryEmitWindowTitleDetection();
        }
        finally
        {
            // Restart the timer for the next poll
            if (_pollTimer != null)
            {
                try
                {
                    _pollTimer.Start();
                }
                catch
                {
                    // Timer might be disposed
                }
            }
        }
    }

    private async Task<PlaybackStatus?> GetPlaybackStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"http://localhost:{_port}/requests/status.json");
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var state = root.GetProperty("state").GetString();
            var length = root.TryGetProperty("length", out var lengthElement) ? lengthElement.GetInt64() : 0; // in seconds
            var time = root.TryGetProperty("time", out var timeElement) ? timeElement.GetInt64() : 0; // in seconds
            var position = root.TryGetProperty("position", out var positionElement) ? positionElement.GetDouble() : 0d; // 0..1
            
            // Extract currently playing file path from VLC
            var detectedFilePath = ExtractLocalFilePath(root);
            
            // If continuous monitoring and we detected a new file, notify
            if (_continuousMonitoring && !string.IsNullOrEmpty(detectedFilePath) && detectedFilePath != _currentFilePath)
            {
                _currentFilePath = detectedFilePath;
                NewFileDetected?.Invoke(this, detectedFilePath);
            }
            else if (_continuousMonitoring && string.IsNullOrWhiteSpace(detectedFilePath))
            {
                // Fallback when VLC status is reachable but does not include a usable path.
                TryEmitWindowTitleDetection();
            }

            var progressPercent = length > 0
                ? Math.Round((time / (double)length) * 100, 2)
                : Math.Round(position * 100d, 2);

            if (progressPercent < 0)
            {
                progressPercent = 0;
            }
            else if (progressPercent > 100)
            {
                progressPercent = 100;
            }

            var isPlaying = state == "playing";
            var hasEnded = state == "stopped" && time > 0;

            return new PlaybackStatus
            {
                FilePath = detectedFilePath ?? _currentFilePath,
                ProgressPercent = progressPercent,
                IsPlaying = isPlaying,
                HasEnded = hasEnded,
                LengthSeconds = length,
                TimeSeconds = time
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VLC HTTP error: {ex.Message}");
            if (_continuousMonitoring)
            {
                TryEmitWindowTitleDetection();
            }

            return null;
        }
    }

    private static string? ExtractLocalFilePath(JsonElement root)
    {
        if (!root.TryGetProperty("information", out var info) ||
            !info.TryGetProperty("category", out var category) ||
            !category.TryGetProperty("meta", out var meta))
        {
            return null;
        }

        // Prefer fields that usually contain absolute file paths/URIs.
        var pathKeys = new[] { "uri", "url", "filepath", "path", "filename" };
        foreach (var key in pathKeys)
        {
            if (!meta.TryGetProperty(key, out var valueElement))
            {
                continue;
            }

            var value = valueElement.GetString();
            var normalized = NormalizeToLocalFilePath(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizeToLocalFilePath(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var candidate = rawValue.Trim().Trim('"');

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return null;
            }

            candidate = Uri.UnescapeDataString(uri.LocalPath);
        }
        else if (candidate.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback for malformed file URIs that Uri.TryCreate can reject.
            candidate = candidate.Substring("file://".Length);
            candidate = Uri.UnescapeDataString(candidate);
        }

        candidate = candidate.Replace('/', Path.DirectorySeparatorChar);

        if (!Path.IsPathRooted(candidate))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        _httpClient.Dispose();
    }

    private void TryEmitWindowTitleDetection()
    {
        try
        {
            var vlcProcess = Process.GetProcessesByName("vlc")
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle));

            if (vlcProcess is null)
            {
                return;
            }

            var title = vlcProcess.MainWindowTitle?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var normalizedTitle = title;
            const string suffix = " - VLC media player";
            if (normalizedTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedTitle = normalizedTitle.Substring(0, normalizedTitle.Length - suffix.Length).Trim();
            }

            if (string.IsNullOrWhiteSpace(normalizedTitle) ||
                string.Equals(normalizedTitle, "VLC media player", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedTitle, "vlc", StringComparison.OrdinalIgnoreCase) ||
                normalizedTitle.Length < 3)
            {
                return;
            }

            if (string.Equals(_currentWindowTitle, normalizedTitle, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentWindowTitle = normalizedTitle;
            NewFileDetected?.Invoke(this, $"{TitlePrefix}{normalizedTitle}");
        }
        catch
        {
            // Ignore fallback detection errors
        }
    }
}

public class PlaybackStatus
{
    public string? FilePath { get; set; }
    public double ProgressPercent { get; set; }
    public bool IsPlaying { get; set; }
    public bool HasEnded { get; set; }
    public long LengthSeconds { get; set; }
    public long TimeSeconds { get; set; }
}
