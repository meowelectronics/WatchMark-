using System.IO;
using System.Diagnostics;
using WatchMark.App.Models;

namespace WatchMark.App.Services;

public class LibraryScannerService
{
    private static readonly string[] SupportedExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv"];

    public IEnumerable<MovieItem> Scan(string libraryPath)
    {
        Debug.WriteLine($"LibraryScannerService.Scan called with path: '{libraryPath}'");
        
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            Debug.WriteLine("LibraryScannerService: Path is null/empty/whitespace");
            return [];
        }

        if (!Directory.Exists(libraryPath))
        {
            Debug.WriteLine($"LibraryScannerService: Directory does not exist: {libraryPath}");
            return [];
        }

        var movies = new List<MovieItem>();
        try
        {
            var files = Directory.EnumerateFiles(libraryPath, "*", SearchOption.AllDirectories).ToList();
            Debug.WriteLine($"LibraryScannerService: Found {files.Count} total files");

            int matchCount = 0;
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (!SupportedExtensions.Contains(extension))
                {
                    continue;
                }

                matchCount++;
                Debug.WriteLine($"LibraryScannerService: Match #{matchCount}: {file}");
                movies.Add(new MovieItem
                {
                    Title = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }
            Debug.WriteLine($"LibraryScannerService: Scan complete. Total matches: {matchCount}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LibraryScannerService.Scan Exception: {ex}");
        }
        return movies;
    }
}
