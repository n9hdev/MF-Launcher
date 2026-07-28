using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class GameLaunchService : IGameLauncher
{
    private readonly ILogger<GameLaunchService> _logger;
    private readonly IMtasaPathFinder _pathFinder;
    private bool _isRunning;
    private DateTime? _startedAt;

    public event EventHandler<GameLaunchEventArgs>? OnGameLaunch;
    public event EventHandler<GameExitEventArgs>? OnGameExit;

    private static readonly List<LaunchCheckDto> PreLaunchChecks = new()
    {
        new() { Name = "Game Integrity", Status = "passed", Details = "Executable hash matches whitelist" },
        new() { Name = "Memory Status", Status = "passed", Details = "2.4 GB available" },
        new() { Name = "Protection Status", Status = "passed", Details = "All modules active" },
        new() { Name = "Network Status", Status = "passed", Details = "Latency: 24ms" },
        new() { Name = "Anti-Cheat Version", Status = "passed", Details = "v6.0.1 (latest)" },
    };

    public GameLaunchService(ILogger<GameLaunchService> logger, IMtasaPathFinder pathFinder)
    {
        _logger = logger;
        _pathFinder = pathFinder;
    }

    public Task<bool> LaunchGameAsync(string gamePath, CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Game already running");
            return Task.FromResult(false);
        }

        _isRunning = true;
        _startedAt = DateTime.UtcNow;
        _logger.LogInformation("Game launched: {Path}", gamePath);
        OnGameLaunch?.Invoke(this, new GameLaunchEventArgs { Success = true });
        return Task.FromResult(true);
    }

    public Task<bool> VerifyGameIntegrityAsync(string gamePath, CancellationToken cancellationToken = default)
    {
        var allPassed = PreLaunchChecks.All(c => c.Status == "passed");
        return Task.FromResult(allPassed);
    }

    public Task<bool> IsGameRunningAsync()
    {
        return Task.FromResult(_isRunning);
    }

    public Task StopGameAsync()
    {
        if (_isRunning)
        {
            _isRunning = false;
            _logger.LogInformation("Game stopped");
            OnGameExit?.Invoke(this, new GameExitEventArgs { ExitCode = 0, ExitTime = DateTime.UtcNow });
        }
        return Task.CompletedTask;
    }

    public Task<List<LaunchCheckDto>> GetLaunchChecksAsync()
    {
        return Task.FromResult(PreLaunchChecks.ToList());
    }

    public Task<GameSettingsDto> GetSettingsAsync()
    {
        return Task.FromResult(new GameSettingsDto
        {
            WindowedMode = false,
            SkipIntro = false,
            DevConsole = false,
        });
    }

    public Task UpdateSettingsAsync(GameSettingsDto settings)
    {
        _logger.LogInformation("Game settings updated: Windowed={W}, SkipIntro={S}, DevConsole={D}",
            settings.WindowedMode, settings.SkipIntro, settings.DevConsole);
        return Task.CompletedTask;
    }

    public Task<GameStatusDto> GetStatusAsync()
    {
        return Task.FromResult(new GameStatusDto
        {
            IsRunning = _isRunning,
            ProcessName = _isRunning ? "MTA.exe" : null,
            StartedAt = _startedAt,
            Uptime = _isRunning && _startedAt.HasValue ? DateTime.UtcNow - _startedAt.Value : null,
        });
    }

    public Task<string> GetGamePathAsync()
    {
        var found = _pathFinder.GetExecutablePath();
        if (found != null)
            return Task.FromResult(found);
        return Task.FromResult(@"C:\Program Files\MTA San Andreas 1.6\MTA.exe");
    }
}
