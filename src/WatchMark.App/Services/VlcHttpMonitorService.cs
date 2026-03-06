using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Timers;

namespace WatchMark.App.Services;

public class VlcHttpMonitorService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly System.Timers.Timer _pollTimer;
    private readonly string _password;
    private readonly int _port;
    private string? _currentFilePath;

    public event EventHandler<PlaybackStatus>? StatusChanged;

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

    public void StartMonitoring(string filePath)
    {
        _currentFilePath = filePath;
        _pollTimer.Start();
    }

    public void StopMonitoring()
    {
        _pollTimer.Stop();
        _currentFilePath = null;
    }

    private async void OnPollTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var status = await GetPlaybackStatusAsync();
            if (status is not null)
            {
                StatusChanged?.Invoke(this, status);
            }
        }
        catch (Exception ex)
        {
            // VLC might not be running or HTTP interface not available
            System.Diagnostics.Debug.WriteLine($"VLC polling error: {ex.Message}");
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
            var length = root.GetProperty("length").GetInt64(); // in seconds
            var time = root.GetProperty("time").GetInt64(); // in seconds

            if (length <= 0)
            {
                return null;
            }

            var progressPercent = Math.Round((time / (double)length) * 100, 2);
            var isPlaying = state == "playing";
            var hasEnded = state == "stopped" && time > 0;

            return new PlaybackStatus
            {
                FilePath = _currentFilePath,
                ProgressPercent = progressPercent,
                IsPlaying = isPlaying,
                HasEnded = hasEnded,
                LengthSeconds = length,
                TimeSeconds = time
            };
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
