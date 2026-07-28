namespace AntiCheat.Core.Interfaces;

public interface IWhitelistProvider
{
    Task<bool> IsProcessWhitelistedAsync(string processName, CancellationToken ct = default);
    Task<bool> IsDriverWhitelistedAsync(string driverName, string? filePath = null, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
}
