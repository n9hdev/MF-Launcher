namespace AntiCheat.Core.Interfaces;

public interface IUpdateService
{
    Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task DownloadUpdateAsync(IProgress<double> progress, CancellationToken cancellationToken = default);
    Task InstallUpdateAsync();
    string? CurrentVersion { get; }
    string? AvailableVersion { get; }
    bool IsUpdateAvailable { get; }
}
