namespace AntiCheat.Core.Interfaces;

public interface IMtasaPathFinder
{
    string? FindPath();
    string? GetExecutablePath(string? mtasaRoot = null);
    void CachePath(string path);
    string? GetCachedPath();
}
