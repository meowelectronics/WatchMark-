using CommunityToolkit.Mvvm.ComponentModel;

namespace WatchMark.App.Models;

public partial class MovieItem : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private TimeSpan duration;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private bool isWatched;

    [ObservableProperty]
    private DateTimeOffset? lastWatchedUtc;

    public string DurationText => duration.TotalMinutes > 0
        ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
        : string.Empty;

    public string ProgressText => $"{progressPercent:F1}%";

    public string LastWatchedText => lastWatchedUtc.HasValue
        ? lastWatchedUtc.Value.ToLocalTime().ToString("g")
        : "Never";
}
