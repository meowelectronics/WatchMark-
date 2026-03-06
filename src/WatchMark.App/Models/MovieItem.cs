using CommunityToolkit.Mvvm.ComponentModel;

namespace WatchMark.App.Models;

public partial class MovieItem : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private TimeSpan duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double progressPercent;

    [ObservableProperty]
    private bool isWatched;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastWatchedText))]
    private DateTimeOffset? lastWatchedUtc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(LastPositionText))]
    private long timeSeconds;

    public string DurationText => duration.TotalMinutes > 0
        ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
        : string.Empty;

    public string ProgressText => $"{progressPercent:F1}%";

    public string LastPositionText
    {
        get
        {
            if (timeSeconds > 0)
            {
                var currentTime = TimeSpan.FromSeconds(timeSeconds);
                return $"{(int)currentTime.TotalHours:D2}:{currentTime.Minutes:D2}:{currentTime.Seconds:D2}";
            }
            return string.Empty;
        }
    }

    public string LastWatchedText => lastWatchedUtc.HasValue
        ? lastWatchedUtc.Value.ToLocalTime().ToString("g")
        : "Never";
}
