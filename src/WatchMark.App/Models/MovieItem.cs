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

    public string DurationText => duration.TotalMinutes > 0
        ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
        : string.Empty;

    public string ProgressText => $"{progressPercent:F1}%";

    public string LastWatchedText => lastWatchedUtc.HasValue
        ? lastWatchedUtc.Value.ToLocalTime().ToString("g")
        : "Never";
}
