using LibVLCSharp.Shared;
using WatchMark.App.Models;

namespace WatchMark.App.Services;

public class VlcPlaybackTrackerService : IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    public event EventHandler<double>? ProgressChanged;
    public event EventHandler? PlaybackEnded;

    public VlcPlaybackTrackerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.TimeChanged += (_, _) => RaiseProgress();
        _mediaPlayer.EndReached += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void LoadAndPlay(string filePath)
    {
        Stop();
        using var media = new Media(_libVlc, filePath, FromType.FromPath);
        _mediaPlayer.Media = media;
        _mediaPlayer.Play();
    }

    public void StartTracking(string filePath)
    {
        LoadAndPlay(filePath);
        _mediaPlayer.Mute = true;
    }

    public void Pause() => _mediaPlayer.Pause();

    public void Stop() => _mediaPlayer.Stop();

    public bool IsPlaying => _mediaPlayer.IsPlaying;

    public void UpdateMovieProgress(MovieItem movie, int watchedThresholdPercent)
    {
        movie.ProgressPercent = GetProgressPercent();
        if (movie.ProgressPercent >= watchedThresholdPercent)
        {
            movie.IsWatched = true;
            movie.LastWatchedUtc = DateTimeOffset.UtcNow;
        }
    }

    private double GetProgressPercent()
    {
        var position = _mediaPlayer.Position;
        if (position < 0)
        {
            return 0;
        }

        return Math.Round(position * 100d, 2);
    }

    private void RaiseProgress()
    {
        ProgressChanged?.Invoke(this, GetProgressPercent());
    }

    public void Dispose()
    {
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}
