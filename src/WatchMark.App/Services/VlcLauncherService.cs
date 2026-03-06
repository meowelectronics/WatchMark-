using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WatchMark.App.Services;

public class VlcLauncherService
{
    private readonly string? _configuredVlcPath;

    public VlcLauncherService(string? configuredVlcPath = null)
    {
        _configuredVlcPath = configuredVlcPath;
    }

    public bool TryOpen(string movieFilePath, out string errorMessage, int httpPort = 8080, string httpPassword = "vlchttp", long startTimeSeconds = 0)
    {
        errorMessage = string.Empty;

        if (!File.Exists(movieFilePath))
        {
            errorMessage = $"File not found: {movieFilePath}";
            return false;
        }

        try
        {
            var vlcExecutable = ResolveVlcExecutablePath();
            var startTimeArg = startTimeSeconds > 0 ? $" --start-time={startTimeSeconds}" : string.Empty;
            var arguments = $"--extraintf=http --http-password={httpPassword} --http-port={httpPort}{startTimeArg} \"{movieFilePath}\"";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = vlcExecutable,
                Arguments = arguments,
                UseShellExecute = false
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            try
            {
               var defaultStartInfo = new ProcessStartInfo
                {
                    FileName = movieFilePath,
                    UseShellExecute = true
                };

                Process.Start(defaultStartInfo);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to open movie: {ex.Message}";
                return false;
            }
        }
    }

    private static string ResolveDefaultVlcExecutablePath()
    {
        var fromRegistry = ReadVlcPathFromRegistry();
        if (!string.IsNullOrWhiteSpace(fromRegistry) && File.Exists(fromRegistry))
        {
            return fromRegistry;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
        };

        var existingPath = candidates.FirstOrDefault(File.Exists);
        return existingPath ?? "vlc";
    }

    private string ResolveVlcExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_configuredVlcPath) && File.Exists(_configuredVlcPath))
        {
            return _configuredVlcPath;
        }

        return ResolveDefaultVlcExecutablePath();
    }

#pragma warning disable CA1416 // Validate platform compatibility (Windows-only WPF app)
    private static string? ReadVlcPathFromRegistry()
    {
        var appPathKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\vlc.exe",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\vlc.exe"
        };

        foreach (var key in appPathKeys)
        {
            var value = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{key}", string.Empty, null) as string
                        ?? Registry.GetValue($@"HKEY_CURRENT_USER\{key}", string.Empty, null) as string;

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var installDir = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\VideoLAN\VLC", "InstallDir", null) as string
                         ?? Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\VideoLAN\VLC", "InstallDir", null) as string;

        if (!string.IsNullOrWhiteSpace(installDir))
        {
            return Path.Combine(installDir, "vlc.exe");
        }

        return null;
    }
#pragma warning restore CA1416
}
