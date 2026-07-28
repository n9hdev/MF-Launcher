using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class WhitelistProvider : IWhitelistProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhitelistProvider> _logger;
    private List<WhitelistEntryEntity>? _cached;
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly object _lock = new();

    public WhitelistProvider(IServiceScopeFactory scopeFactory, ILogger<WhitelistProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private async Task<List<WhitelistEntryEntity>> GetEntriesAsync(CancellationToken ct = default)
    {
        if (_cached != null && DateTime.UtcNow - _lastRefresh < CacheDuration)
            return _cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.WhitelistEntries.ToListAsync(ct);
        lock (_lock)
        {
            _cached = entries;
            _lastRefresh = DateTime.UtcNow;
        }
        return _cached;
    }

    public async Task<bool> IsProcessWhitelistedAsync(string processName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var name = processName.Trim();
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name += ".exe";

        var entries = await GetEntriesAsync(ct);
        return entries.Any(e =>
            e.Type.Equals("Process", StringComparison.OrdinalIgnoreCase) &&
            e.Entry.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsDriverWhitelistedAsync(string driverName, string? filePath = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driverName))
            return false;

        var entries = await GetEntriesAsync(ct);

        if (entries.Any(e =>
                e.Type.Equals("Process", StringComparison.OrdinalIgnoreCase) &&
                e.Entry.Equals(driverName, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            foreach (var entry in entries.Where(e => e.Type.Equals("Path", StringComparison.OrdinalIgnoreCase)))
            {
                var pattern = entry.Entry.TrimEnd('*').TrimEnd('\\');
                if (filePath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.WhitelistEntries.ToListAsync(ct);
        lock (_lock)
        {
            _cached = entries;
            _lastRefresh = DateTime.UtcNow;
        }
    }
}
