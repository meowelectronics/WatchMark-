using System.IO;
using Microsoft.Data.Sqlite;
using WatchMark.App.Models;

namespace WatchMark.App.Services;

public class WatchStatusRepository
{
    private readonly string _databasePath;

    public WatchStatusRepository(string databasePath)
    {
        _databasePath = databasePath;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS WatchStatus (
                FilePath TEXT PRIMARY KEY,
                ProgressPercent REAL NOT NULL,
                IsWatched INTEGER NOT NULL,
                LastWatchedUtc TEXT
            )";
        createTableCommand.ExecuteNonQuery();
    }

    public void Save(MovieItem movie)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO WatchStatus (FilePath, ProgressPercent, IsWatched, LastWatchedUtc)
            VALUES ($filePath, $progressPercent, $isWatched, $lastWatchedUtc)
            ON CONFLICT(FilePath) DO UPDATE SET
                ProgressPercent = $progressPercent,
                IsWatched = $isWatched,
                LastWatchedUtc = $lastWatchedUtc";

        command.Parameters.AddWithValue("$filePath", movie.FilePath);
        command.Parameters.AddWithValue("$progressPercent", movie.ProgressPercent);
        command.Parameters.AddWithValue("$isWatched", movie.IsWatched ? 1 : 0);
        command.Parameters.AddWithValue("$lastWatchedUtc", movie.LastWatchedUtc?.ToString("o") ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    public Dictionary<string, (double ProgressPercent, bool IsWatched, DateTimeOffset? LastWatchedUtc)> LoadAll()
    {
        var result = new Dictionary<string, (double, bool, DateTimeOffset?)>();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT FilePath, ProgressPercent, IsWatched, LastWatchedUtc FROM WatchStatus";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var filePath = reader.GetString(0);
            var progressPercent = reader.GetDouble(1);
            var isWatched = reader.GetInt32(2) == 1;
            var lastWatchedUtc = reader.IsDBNull(3)
                ? null
                : (DateTimeOffset?)DateTimeOffset.Parse(reader.GetString(3));

            result[filePath] = (progressPercent, isWatched, lastWatchedUtc);
        }

        return result;
    }
}
