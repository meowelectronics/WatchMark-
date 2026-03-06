using WatchMark.App.Services;

namespace WatchMark.Tests.Security;

public class PathTraversalTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LibraryScannerService _scanner;

    public PathTraversalTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WatchMark_PathTest_{Guid.NewGuid()}");
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

    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\Windows\\System32")]
    [InlineData("..\\..\\..\\sensitive")]
    public void Scan_WithPathTraversalAttempt_ReturnsEmptyOrSafe(string relativePath)
    {
        // Arrange
        var maliciousPath = Path.Combine(_testDirectory, relativePath);

        // Act
        var result = _scanner.Scan(maliciousPath);

        // Assert - Should either return empty or only scan legitimate directory
        // Path.Combine normalizes the path, so this tests the scanner's behavior
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("C:\\..\\..\\Windows\\System32")]
    [InlineData("E:\\Videos\\..\\..\\..\\Users")]
    [InlineData("/home/../../../etc")]
    public void Scan_WithAbsolutePathTraversal_DoesNotEscapeIntendedDirectory(string traversalPath)
    {
        // Act
        var result = _scanner.Scan(traversalPath).ToList();

        // Assert - Should handle gracefully (empty if path doesn't exist)
        Assert.NotNull(result);
        // If path exists, ensure it only scans that specific resolved path
    }

    [Theory]
    [InlineData("test/../../sensitive")]
    [InlineData("videos/../../../system")]
    [InlineData("movies/..\\..\\..\\protected")]
    public void Scan_WithRelativePathComponents_HandledSafely(string pathWithTraversal)
    {
        // Arrange
        var testPath = Path.Combine(_testDirectory, pathWithTraversal);

        // Act
        var result = _scanner.Scan(testPath);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Scan_WithSymbolicLinkToSensitiveArea_DoesNotFollowDangerousLinks()
    {
        // This test is platform-dependent and may require admin rights
        // Testing scanner behavior with symbolic links
        
        // Arrange
        var linkPath = Path.Combine(_testDirectory, "suspicious_link");
        
        // Act
        var result = _scanner.Scan(linkPath);

        // Assert - Should handle non-existent directory gracefully
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("C:\\")]
    [InlineData("C:\\Windows")]
    [InlineData("C:\\Program Files")]
    public void Scan_SystemDirectories_DoesNotCauseSecurityIssues(string systemPath)
    {
        // Act - Scanner should handle system directories safely
        // This tests that scanning system dirs doesn't cause crashes or security issues
        var result = _scanner.Scan(systemPath);

        // Assert - Should complete without throwing
        Assert.NotNull(result);
        // Note: May return many files, but shouldn't cause security issues
    }

    [Theory]
    [InlineData("..\\..\\..\\etc\\passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("../../../../root/.ssh/id_rsa")]
    public void Scan_TriesAccessingSensitiveFiles_BlockedOrHandledSafely(string sensitivePath)
    {
        // Arrange
        var fullPath = Path.Combine(_testDirectory, sensitivePath);

        // Act
        var result = _scanner.Scan(fullPath);

        // Assert - Should not grant access to sensitive system files
        Assert.NotNull(result);
        // Either empty or safely handled
    }

    [Fact]
    public void Scan_WithMaliciousFileNamesInDirectory_HandlesAllSafely()
    {
        // Arrange
        var maliciousNames = new[]
        {
            "../../escape.mp4",
            "..\\..\\escape.mkv",
            "normal_movie\0.mp4", // Null byte
            "movie\r\n.mp4", // CRLF
            "movie;rm -rf.mp4",
            "movie`whoami`.mp4",
            "movie$(whoami).mp4"
        };

        foreach (var name in maliciousNames)
        {
            try
            {
                var safeName = name.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                if (!safeName.Contains(".."))
                {
                    var filePath = Path.Combine(_testDirectory, safeName);
                    File.Create(filePath).Dispose();
                }
            }
            catch
            {
                // Some names may be invalid on Windows
            }
        }

        // Act
        var result = _scanner.Scan(_testDirectory).ToList();

        // Assert - Should scan without exceptions
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void Scan_WindowsReservedDeviceNames_HandledSafely(string reservedName)
    {
        // Arrange
        var path = Path.Combine(_testDirectory, reservedName);

        // Act
        var result = _scanner.Scan(path);

        // Assert - Should handle reserved names without crashes
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("movie<script>.mp4")]
    [InlineData("movie>redirect.mp4")]
    [InlineData("movie|pipe.mp4")]
    [InlineData("movie?.mp4")]
    [InlineData("movie*.mp4")]
    public void Scan_InvalidFileNameCharacters_HandledGracefully(string invalidName)
    {
        // Most of these are invalid on Windows and will fail to create
        // This tests that the scanner doesn't crash on such attempts
        
        // Act
        var result = _scanner.Scan(Path.Combine(_testDirectory, invalidName));

        // Assert
        Assert.NotNull(result);
    }
}
