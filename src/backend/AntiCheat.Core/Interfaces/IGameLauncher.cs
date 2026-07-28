using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IGameLauncher
{
    Task<bool> LaunchGameAsync(string gamePath, CancellationToken cancellationToken = default);
    Task<bool> VerifyGameIntegrityAsync(string gamePath, CancellationToken cancellationToken = default);
    Task<bool> IsGameRunningAsync();
    Task StopGameAsync();
    Task<List<LaunchCheckDto>> GetLaunchChecksAsync();
    Task<GameSettingsDto> GetSettingsAsync();
    Task UpdateSettingsAsync(GameSettingsDto settings);
    Task<GameStatusDto> GetStatusAsync();
    Task<string> GetGamePathAsync();
    event EventHandler<GameLaunchEventArgs>? OnGameLaunch;
    event EventHandler<GameExitEventArgs>? OnGameExit;
}

public class GameLaunchEventArgs : EventArgs
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GameExitEventArgs : EventArgs
{
    public int ExitCode { get; set; }
    public DateTime ExitTime { get; set; }
}
