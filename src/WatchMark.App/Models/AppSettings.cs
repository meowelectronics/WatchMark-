using System.Collections.Generic;

namespace WatchMark.App.Models;

public class AppSettings
{
    public int WatchedThresholdPercent { get; set; } = 90;
    public string LibraryPath { get; set; } = "C:\\Movies";
    public string DatabasePath { get; set; } = "data\\watchstatus.db";
    public string? VlcPath { get; set; }
    public List<string> RecentLibraryPaths { get; set; } = new();
}
