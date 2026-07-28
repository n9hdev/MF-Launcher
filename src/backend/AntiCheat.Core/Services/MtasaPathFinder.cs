using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class MtasaPathFinder : IMtasaPathFinder
{
    private readonly ILogger<MtasaPathFinder> _logger;

    private static readonly string[] MtasaExecutables =
    {
        "Multi Theft Auto.exe", "multi-theft-auto.exe",
        "mta-client.exe", "mta-sa.exe", "gta_sa.exe"
    };

    private static readonly string[] MtasaKeyFiles =
    {
        "Multi Theft Auto.exe", "multi-theft-auto.exe",
        "mta-client.exe", "gta_sa.exe",
        "mta.conf", "clientRegistry.xml"
    };

    public MtasaPathFinder(ILogger<MtasaPathFinder> logger)
    {
        _logger = logger;
    }

    public string? FindPath()
    {
        var cached = GetCachedPath();
        if (cached != null) return cached;

        _logger.LogInformation("Searching for MTASA installation...");

        string? path;

        path = SearchMtasaRegistry();
        if (path != null) { CachePath(path); return path; }

        path = SearchUninstallRegistry();
        if (path != null) { CachePath(path); return path; }

        path = SearchCommonPaths();
        if (path != null) { CachePath(path); return path; }

        path = SearchProgramFiles();
        if (path != null) { CachePath(path); return path; }

        path = SearchUserDirectories();
        if (path != null) { CachePath(path); return path; }

        path = SearchDesktopAndDownloads();
        if (path != null) { CachePath(path); return path; }

        path = SearchShortcutsAndFolders();
        if (path != null) { CachePath(path); return path; }

        _logger.LogWarning("MTASA not found on this system");
        return null;
    }

    public string? GetExecutablePath(string? mtasaRoot = null)
    {
        if (string.IsNullOrEmpty(mtasaRoot))
            mtasaRoot = FindPath();

        if (string.IsNullOrEmpty(mtasaRoot) || !Directory.Exists(mtasaRoot))
            return null;

        foreach (var exe in MtasaExecutables)
        {
            var fullPath = Path.Combine(mtasaRoot, exe);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }

    public void CachePath(string path)
    {
        try
        {
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MFCITYAntiCheat");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "mtasa_cache.txt"), path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cache MTASA path");
        }
    }

    public string? GetCachedPath()
    {
        try
        {
            var cacheFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MFCITYAntiCheat", "mtasa_cache.txt");
            if (File.Exists(cacheFile))
            {
                var cachedPath = File.ReadAllText(cacheFile).Trim();
                if (Directory.Exists(cachedPath) && VerifyMtasaPath(cachedPath))
                    return cachedPath;
            }
        }
        catch { }
        return null;
    }

    private string? SearchMtasaRegistry()
    {
        const string regPath = @"SOFTWARE\WOW6432Node\Multi Theft Auto: San Andreas All\1.6";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(regPath);
            if (key == null) return null;

            var lastRunPath = key.GetValue("Last Run Path")?.ToString();
            if (!string.IsNullOrEmpty(lastRunPath))
            {
                var dir = Path.GetDirectoryName(lastRunPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }

            var lastInstall = key.GetValue("Last Install Location")?.ToString();
            if (!string.IsNullOrEmpty(lastInstall) && Directory.Exists(lastInstall))
                return lastInstall;

            var lastRunLocation = key.GetValue("Last Run Location")?.ToString();
            if (!string.IsNullOrEmpty(lastRunLocation) && Directory.Exists(lastRunLocation))
                return lastRunLocation;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MTASA registry search failed");
        }
        return null;
    }

    private string? SearchUninstallRegistry()
    {
        var regPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var regPath in regPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? "";
                        var installLocation = subKey?.GetValue("InstallLocation")?.ToString() ?? "";

                        if ((displayName.Contains("Multi Theft Auto", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("MTA: San Andreas", StringComparison.OrdinalIgnoreCase)) &&
                            !string.IsNullOrEmpty(installLocation) && VerifyMtasaPath(installLocation))
                        {
                            return installLocation;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        return null;
    }

    private string? SearchCommonPaths()
    {
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Multi Theft Auto"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Multi Theft Auto"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MTA"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MTA"),
            @"C:\Games\MTA", @"C:\Games\Multi Theft Auto",
            @"D:\Games\MTA", @"D:\Games\Multi Theft Auto",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTA"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MTA")
        };

        foreach (var path in commonPaths)
        {
            if (VerifyMtasaPath(path))
                return path;
        }
        return null;
    }

    private string? SearchProgramFiles()
    {
        var programDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var baseDir in programDirs)
        {
            try
            {
                var result = SearchDirectoryRecursive(baseDir, 2);
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    private string? SearchUserDirectories()
    {
        var userDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        foreach (var dir in userDirs)
        {
            try
            {
                var result = SearchDirectoryRecursive(dir, 3);
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    private string? SearchDesktopAndDownloads()
    {
        var portableDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive\\Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive\\Downloads")
        };

        foreach (var dir in portableDirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                var result = SearchDirectoryRecursive(dir, 2);
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    private string? SearchShortcutsAndFolders()
    {
        var locations = new List<string>();
        try { locations.Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)); } catch { }
        try { locations.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)); } catch { }
        try { locations.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); } catch { }
        try { locations.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")); } catch { }
        try { locations.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive\\Desktop")); } catch { }
        try { locations.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)); } catch { }
        try { locations.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)); } catch { }
        try { locations.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Multi Theft Auto")); } catch { }
        try { locations.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Multi Theft Auto")); } catch { }
        try { locations.Add(@"C:\"); } catch { }
        try { locations.Add(@"D:\"); } catch { }

        foreach (var location in locations)
        {
            if (!Directory.Exists(location)) continue;
            try
            {
                foreach (var shortcut in DirectorySearchWithTimeout(location, "*.lnk", 2))
                {
                    try
                    {
                        var name = Path.GetFileName(shortcut);
                        if (!name.Contains("MTA", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Multi", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("San Andreas", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var target = ResolveShortcut(shortcut);
                        if (target != null)
                        {
                            var dir = Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                return dir;
                        }
                    }
                    catch { }
                }

                foreach (var folder in Directory.GetDirectories(location))
                {
                    try
                    {
                        var name = Path.GetFileName(folder);
                        if (name.Contains("MTA", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Multi", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("San Andreas", StringComparison.OrdinalIgnoreCase))
                        {
                            if (VerifyMtasaPath(folder)) return folder;
                            foreach (var sub in Directory.GetDirectories(folder))
                            {
                                if (VerifyMtasaPath(sub)) return sub;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        return null;
    }

    private string? ResolveShortcut(string shortcutPath)
    {
        try
        {
            if (!File.Exists(shortcutPath)) return null;
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string targetPath = shortcut.TargetPath;
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                return targetPath;
        }
        catch { }
        return null;
    }

    private List<string> DirectorySearchWithTimeout(string rootPath, string pattern, int maxDepth)
    {
        var results = new List<string>();
        try { SearchDirectoryWithDepth(rootPath, pattern, maxDepth, 0, results); } catch { }
        return results;
    }

    private void SearchDirectoryWithDepth(string path, string pattern, int maxDepth, int currentDepth, List<string> results)
    {
        if (currentDepth >= maxDepth) return;
        try
        {
            results.AddRange(Directory.GetFiles(path, pattern));
            foreach (var dir in Directory.GetDirectories(path))
            {
                try { SearchDirectoryWithDepth(dir, pattern, maxDepth, currentDepth + 1, results); } catch { }
            }
        }
        catch { }
    }

    private string? SearchDirectoryRecursive(string rootPath, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth || !Directory.Exists(rootPath)) return null;
        try
        {
            if (VerifyMtasaPath(rootPath)) return rootPath;
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                try
                {
                    var dirName = new DirectoryInfo(dir).Name;
                    if (dirName.Contains("MTA", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Contains("Multi", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Contains("GTA", StringComparison.OrdinalIgnoreCase))
                    {
                        if (VerifyMtasaPath(dir)) return dir;
                    }
                    var result = SearchDirectoryRecursive(dir, maxDepth, currentDepth + 1);
                    if (result != null) return result;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    private bool VerifyMtasaPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
        return MtasaKeyFiles.Count(f => File.Exists(Path.Combine(path, f))) >= 2;
    }
}
