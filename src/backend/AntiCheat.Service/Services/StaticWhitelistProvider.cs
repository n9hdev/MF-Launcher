using AntiCheat.Core.Interfaces;

namespace AntiCheat.Service.Services;

public class StaticWhitelistProvider : IWhitelistProvider
{
    private readonly HashSet<string> _processes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gta_sa.exe",
        "MTA.exe",
        "Multi Theft Auto.exe",
        "MTA San Andreas.exe",
        "Discord.exe",
        "Steam.exe",
        "chrome.exe",
        "firefox.exe",
        "msedge.exe",
        "explorer.exe",
        "svchost.exe",
        "RuntimeBroker.exe",
        "SearchIndexer.exe",
        "sihost.exe",
        "taskhostw.exe",
        "Widgets.exe",
        "conhost.exe",
        "cmd.exe",
        "powershell.exe",
        "Code.exe",
        "devenv.exe",
    };

    private readonly List<string> _pathPrefixes = new()
    {
        @"C:\Windows\System32\",
        @"C:\Windows\SysWOW64\",
        @"C:\Program Files\",
        @"C:\Program Files (x86)\",
    };

    public Task<bool> IsProcessWhitelistedAsync(string processName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return Task.FromResult(false);

        var name = processName.Trim();
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name += ".exe";

        return Task.FromResult(_processes.Contains(name));
    }

    public Task<bool> IsDriverWhitelistedAsync(string driverName, string? filePath = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driverName))
            return Task.FromResult(false);

        if (_processes.Contains(driverName))
            return Task.FromResult(true);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            foreach (var prefix in _pathPrefixes)
            {
                if (filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
}
