using WatchMark.App.Models;

namespace WatchMark.Tests.Models;

public class MovieItemTests
{
    [Fact]
    public void MovieItem_InitializesWithDefaultValues()
    {
        // Act
        var movie = new MovieItem();

        // Assert
        Assert.Equal(string.Empty, movie.Title);
        Assert.Equal(string.Empty, movie.FilePath);
        Assert.Equal(TimeSpan.Zero, movie.Duration);
        Assert.Equal(0, movie.ProgressPercent);
        Assert.False(movie.IsWatched);
        Assert.Null(movie.LastWatchedUtc);
    }

    [Fact]
    public void MovieItem_CanSetProperties()
    {
        // Arrange
        var movie = new MovieItem();
        var testTime = DateTimeOffset.UtcNow;

        // Act
        movie.Title = "Test Movie";
        movie.FilePath = @"C:\Movies\test.mp4";
        movie.Duration = TimeSpan.FromMinutes(120);
        movie.ProgressPercent = 45.5;
        movie.IsWatched = true;
        movie.LastWatchedUtc = testTime;

        // Assert
        Assert.Equal("Test Movie", movie.Title);
        Assert.Equal(@"C:\Movies\test.mp4", movie.FilePath);
        Assert.Equal(TimeSpan.FromMinutes(120), movie.Duration);
        Assert.Equal(45.5, movie.ProgressPercent);
        Assert.True(movie.IsWatched);
        Assert.Equal(testTime, movie.LastWatchedUtc);
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(90, 0, "1h 30m")]
    [InlineData(120, 0, "2h 0m")]
    [InlineData(135, 0, "2h 15m")]
    [InlineData(45, 0, "0h 45m")]
    public void DurationText_FormatsCorrectly(int minutes, int seconds, string expected)
    {
        // Arrange
        var movie = new MovieItem
        {
            Duration = TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(seconds))
        };

        // Act
        var result = movie.DurationText;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25.5)]
    [InlineData(50.0)]
    [InlineData(75.75)]
    [InlineData(100.0)]
    public void ProgressText_FormatsCorrectly(double progress)
    {
        // Arrange
        var movie = new MovieItem
        {
            ProgressPercent = progress
        };

        // Act
        var result = movie.ProgressText;

        // Assert
        var expected = $"{progress:F1}%";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsWatched_DefaultsToFalse()
    {
        // Arrange & Act
        var movie = new MovieItem();

        // Assert
        Assert.False(movie.IsWatched);
    }

    [Fact]
    public void IsWatched_CanBeSet()
    {
        // Arrange
        var movie = new MovieItem();

        // Act
        movie.IsWatched = true;

        // Assert
        Assert.True(movie.IsWatched);
    }

    [Fact]
    public void ProgressPercent_CanExceed100()
    {
        // Arrange
        var movie = new MovieItem();

        // Act
        movie.ProgressPercent = 150.0;

        // Assert
        Assert.Equal(150.0, movie.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_CanBeNegative()
    {
        // Arrange
        var movie = new MovieItem();

        // Act
        movie.ProgressPercent = -10.0;

        // Assert
        Assert.Equal(-10.0, movie.ProgressPercent);
    }

    [Fact]
    public void LastWatchedUtc_CanBeNull()
    {
        // Arrange & Act
        var movie = new MovieItem
        {
            LastWatchedUtc = null
        };

        // Assert
        Assert.Null(movie.LastWatchedUtc);
    }

    [Fact]
    public void LastWatchedUtc_CanBeSet()
    {
        // Arrange
        var movie = new MovieItem();
        var testDate = new DateTimeOffset(2026, 3, 6, 10, 30, 0, TimeSpan.Zero);

        // Act
        movie.LastWatchedUtc = testDate;

        // Assert
        Assert.Equal(testDate, movie.LastWatchedUtc);
    }
}
