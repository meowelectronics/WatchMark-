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
                LastWatchedUtc TEXT,
                Duration REAL DEFAULT 0,
                TimeSeconds INTEGER DEFAULT 0
            )";
        createTableCommand.ExecuteNonQuery();

        // Add columns if they don't exist (migration)
        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "PRAGMA table_info(WatchStatus)";
        var columns = new List<string>();
        using (var reader = alterCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("Duration"))
        {
            var addDurationCmd = connection.CreateCommand();
            addDurationCmd.CommandText = "ALTER TABLE WatchStatus ADD COLUMN Duration REAL DEFAULT 0";
            addDurationCmd.ExecuteNonQuery();
        }

        if (!columns.Contains("TimeSeconds"))
        {
            var addTimeCmd = connection.CreateCommand();
            addTimeCmd.CommandText = "ALTER TABLE WatchStatus ADD COLUMN TimeSeconds INTEGER DEFAULT 0";
            addTimeCmd.ExecuteNonQuery();
        }
    }

    public void Save(MovieItem movie)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO WatchStatus (FilePath, ProgressPercent, IsWatched, LastWatchedUtc, Duration, TimeSeconds)
            VALUES ($filePath, $progressPercent, $isWatched, $lastWatchedUtc, $duration, $timeSeconds)
            ON CONFLICT(FilePath) DO UPDATE SET
                ProgressPercent = $progressPercent,
                IsWatched = $isWatched,
                LastWatchedUtc = $lastWatchedUtc,
                Duration = $duration,
                TimeSeconds = $timeSeconds";

        command.Parameters.AddWithValue("$filePath", movie.FilePath);
        command.Parameters.AddWithValue("$progressPercent", movie.ProgressPercent);
        command.Parameters.AddWithValue("$isWatched", movie.IsWatched ? 1 : 0);
        command.Parameters.AddWithValue("$lastWatchedUtc", movie.LastWatchedUtc?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$duration", movie.Duration.TotalSeconds);
        command.Parameters.AddWithValue("$timeSeconds", movie.TimeSeconds);

        command.ExecuteNonQuery();
    }

    public Dictionary<string, (double ProgressPercent, bool IsWatched, DateTimeOffset? LastWatchedUtc, double Duration, long TimeSeconds)> LoadAll()
    {
        var result = new Dictionary<string, (double, bool, DateTimeOffset?, double, long)>();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT FilePath, ProgressPercent, IsWatched, LastWatchedUtc, Duration, TimeSeconds FROM WatchStatus";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var filePath = reader.GetString(0);
            var progressPercent = reader.GetDouble(1);
            var isWatched = reader.GetInt32(2) == 1;
            var lastWatchedUtc = reader.IsDBNull(3)
                ? null
                : (DateTimeOffset?)DateTimeOffset.Parse(reader.GetString(3));
            var duration = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);
            var timeSeconds = reader.IsDBNull(5) ? 0L : reader.GetInt64(5);

            result[filePath] = (progressPercent, isWatched, lastWatchedUtc, duration, timeSeconds);
        }

        return result;
    }
}
