using WatchMark.App.Models;
using WatchMark.App.Services;

namespace WatchMark.Tests.Services;

public class LibraryScannerServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LibraryScannerService _scanner;

    public LibraryScannerServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WatchMark_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _scanner = new LibraryScannerService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void Scan_WithNullPath_ReturnsEmptyList()
    {
        // Act
        var result = _scanner.Scan(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Scan_WithEmptyPath_ReturnsEmptyList()
    {
        // Act
        var result = _scanner.Scan(string.Empty);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Scan_WithNonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "NonExistent");

        // Act
        var result = _scanner.Scan(nonExistentPath);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Scan_WithSupportedVideoFiles_ReturnsMovieItems()
    {
        // Arrange
        CreateTestFile("movie1.mp4");
        CreateTestFile("movie2.mkv");
        CreateTestFile("movie3.avi");

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, m => m.Title == "movie1");
        Assert.Contains(result, m => m.Title == "movie2");
        Assert.Contains(result, m => m.Title == "movie3");
    }

    [Fact]
    public void Scan_WithUnsupportedFiles_IgnoresThem()
    {
        // Arrange
        CreateTestFile("movie.mp4");
        CreateTestFile("document.txt");
        CreateTestFile("image.jpg");

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("movie", result[0].Title);
    }

    [Fact]
    public void Scan_WithSubdirectories_FindsAllVideos()
    {
        // Arrange
        CreateTestFile("movie1.mp4");
        var subDir = Path.Combine(_testDirectory, "subfolder");
        Directory.CreateDirectory(subDir);
        File.Create(Path.Combine(subDir, "movie2.mkv")).Dispose();

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    public void Scan_SupportsExpectedVideoFormats(string extension)
    {
        // Arrange
        CreateTestFile($"test{extension}");

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Scan_ExtractsCorrectTitle()
    {
        // Arrange
        CreateTestFile("The Matrix (1999).mp4");

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("The Matrix (1999)", result[0].Title);
    }

    [Fact]
    public void Scan_StoresCorrectFilePath()
    {
        // Arrange
        var fileName = "movie.mp4";
        CreateTestFile(fileName);
        var expectedPath = Path.Combine(_testDirectory, fileName);

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedPath, result[0].FilePath);
    }

    private void CreateTestFile(string fileName)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.Create(filePath).Dispose();
    }
}
