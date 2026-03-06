using WatchMark.App.Models;
using WatchMark.App.Services;

namespace WatchMark.Tests.Security;

public class SqlInjectionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly WatchStatusRepository _repository;

    public SqlInjectionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"WatchMark_SecurityTest_{Guid.NewGuid()}.db");
        _repository = new WatchStatusRepository(_testDbPath);
    }

    public void Dispose()
    {
        if (!File.Exists(_testDbPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(_testDbPath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    [Theory]
    [InlineData("'; DROP TABLE WatchStatus; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DELETE FROM WatchStatus WHERE '1'='1")]
    [InlineData("' UNION SELECT * FROM WatchStatus --")]
    [InlineData("admin'--")]
    [InlineData("' OR 1=1--")]
    [InlineData("1' OR '1' = '1")]
    public void Save_WithSqlInjectionAttemptInFilePath_ShouldNotExecuteMaliciousCode(string maliciousPath)
    {
        // Arrange
        var movie = new MovieItem
        {
            FilePath = maliciousPath,
            Title = "Test Movie",
            ProgressPercent = 50.0,
            IsWatched = false
        };

        // Act - Save should treat the malicious input as literal text
        _repository.Save(movie);

        // Assert - Load the data back and verify table still exists and data is safe
        var loaded = _repository.LoadAll();
        Assert.Contains(maliciousPath, loaded.Keys);
        Assert.Equal(50.0, loaded[maliciousPath].ProgressPercent);
    }

    [Theory]
    [InlineData("C:\\Movies\\test'; DROP TABLE WatchStatus; --.mp4")]
    [InlineData("E:\\Videos\\movie' OR '1'='1.mkv")]
    [InlineData("/home/user/'; DELETE FROM WatchStatus; --.avi")]
    public void Save_WithSqlInjectionInRealisticFilePath_TreatsAsLiteralString(string maliciousFilePath)
    {
        // Arrange
        var movie = new MovieItem
        {
            FilePath = maliciousFilePath,
            Title = "Malicious Test",
            ProgressPercent = 75.0,
            IsWatched = true
        };

        // Act
        _repository.Save(movie);

        // Assert - Should be stored as-is without executing SQL
        var loaded = _repository.LoadAll();
        Assert.Single(loaded);
        Assert.True(loaded.ContainsKey(maliciousFilePath));
    }

    [Fact]
    public void Save_MultipleMaliciousInputs_AllStoredSafely()
    {
        // Arrange
        var maliciousMovies = new[]
        {
            new MovieItem { FilePath = "'; DROP TABLE WatchStatus; --", ProgressPercent = 10 },
            new MovieItem { FilePath = "' OR '1'='1", ProgressPercent = 20 },
            new MovieItem { FilePath = "admin'--", ProgressPercent = 30 },
        };

        // Act
        foreach (var movie in maliciousMovies)
        {
            _repository.Save(movie);
        }

        // Assert
        var loaded = _repository.LoadAll();
        Assert.Equal(3, loaded.Count);
        foreach (var movie in maliciousMovies)
        {
            Assert.Contains(movie.FilePath, loaded.Keys);
        }
    }

    [Theory]
    [InlineData("\"; DROP TABLE WatchStatus; --")]
    [InlineData("\\\" OR \\\"1\\\"=\\\"1")]
    [InlineData("'; EXEC sp_MSForEachTable 'DROP TABLE ?'; --")]
    public void Save_WithVariousSqlInjectionPatterns_StoresLiterally(string maliciousInput)
    {
        // Arrange
        var movie = new MovieItem
        {
            FilePath = maliciousInput,
            ProgressPercent = 100.0,
            IsWatched = true
        };

        // Act
        _repository.Save(movie);

        // Assert
        var loaded = _repository.LoadAll();
        Assert.Single(loaded);
        Assert.Contains(maliciousInput, loaded.Keys);
    }

    [Fact]
    public void LoadAll_AfterSqlInjectionAttempts_TableIntegrityMaintained()
    {
        // Arrange - Save legitimate data first
        var legitimateMovie = new MovieItem
        {
            FilePath = @"C:\Movies\GoodMovie.mp4",
            ProgressPercent = 50.0,
            IsWatched = false
        };
        _repository.Save(legitimateMovie);

        // Act - Try SQL injection
        var maliciousMovie = new MovieItem
        {
            FilePath = "'; DROP TABLE WatchStatus; --",
            ProgressPercent = 0,
            IsWatched = false
        };
        _repository.Save(maliciousMovie);

        // Assert - Both records should exist, table should be intact
        var loaded = _repository.LoadAll();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(legitimateMovie.FilePath, loaded.Keys);
        Assert.Contains(maliciousMovie.FilePath, loaded.Keys);
    }

    [Theory]
    [InlineData("C:\\Movies\\test\0movie.mp4")] // Null byte injection
    [InlineData("C:\\Movies\\test\r\nmovie.mp4")] // CRLF injection
    [InlineData("C:\\Movies\\test\nmovie.mp4")] // Newline injection
    public void Save_WithNullByteAndControlCharacters_HandledSafely(string pathWithControlChars)
    {
        // Arrange
        var movie = new MovieItem
        {
            FilePath = pathWithControlChars,
            ProgressPercent = 25.0
        };

        // Act
        _repository.Save(movie);

        // Assert - Should store and retrieve without issues
        var loaded = _repository.LoadAll();
        Assert.Single(loaded);
    }
}
