using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Launcher;

public class GameLauncherService : IGameLauncher, IDisposable
{
    private readonly ILogger<GameLauncherService> _logger;
    private readonly IMtasaPathFinder _pathFinder;
    private readonly IWhitelistProvider _whitelist;
    private Process? _gameProcess;
    private CancellationTokenSource? _pipeCts;
    private Task? _pipeListenerTask;
    private Task? _monitorTask;
    private bool _disposed;

    private const string PipeName = "MafiaCityAntiCheatV6";
    private const string GameProcessName = "MTA";
    private const string GameExecutable = "MTA.exe";
    private const string DefaultGamePath = @"C:\Program Files\MTA San Andreas 1.6\MTA.exe";
    private const int HeartbeatTimeoutMs = 15000;

    private GameSettingsDto _settings = new();
    private DateTime? _startedAt;
    private DateTime? _lastHeartbeat;

    public event EventHandler<GameLaunchEventArgs>? OnGameLaunch;
    public event EventHandler<GameExitEventArgs>? OnGameExit;

    public GameLauncherService(ILogger<GameLauncherService> logger, IMtasaPathFinder pathFinder, IWhitelistProvider whitelist)
    {
        _logger = logger;
        _pathFinder = pathFinder;
        _whitelist = whitelist;
    }

    public Task<bool> LaunchGameAsync(string gamePath, CancellationToken cancellationToken = default)
    {
        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            _logger.LogWarning("Game already running (PID: {Pid})", _gameProcess.Id);
            return Task.FromResult(false);
        }

        var resolvedPath = ResolveGamePath(gamePath);
        if (!File.Exists(resolvedPath))
        {
            _logger.LogError("Game executable not found: {Path}", resolvedPath);
            OnGameLaunch?.Invoke(this, new GameLaunchEventArgs { Success = false, ErrorMessage = "File not found" });
            return Task.FromResult(false);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                Arguments = GetLaunchArguments(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _gameProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _gameProcess.Start();
            _startedAt = DateTime.UtcNow;

            _gameProcess.Exited += OnGameProcessExited;

            _logger.LogInformation("Game launched: {Path} (PID: {Pid})", resolvedPath, _gameProcess.Id);
            OnGameLaunch?.Invoke(this, new GameLaunchEventArgs { Success = true });

            StartNamedPipeServer();
            StartProcessMonitor();

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game: {Path}", resolvedPath);
            OnGameLaunch?.Invoke(this, new GameLaunchEventArgs { Success = false, ErrorMessage = ex.Message });
            return Task.FromResult(false);
        }
    }

    public Task<bool> VerifyGameIntegrityAsync(string gamePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolveGamePath(gamePath);
        var exists = File.Exists(resolvedPath);

        if (!exists)
        {
            _logger.LogWarning("Game integrity check failed: {Path} not found", resolvedPath);
            return Task.FromResult(false);
        }

        var existingProcesses = Process.GetProcessesByName(GameProcessName);
        var alreadyRunning = existingProcesses.Length > 0;

        _logger.LogInformation("Game integrity: {Path} exists={Exists}, alreadyRunning={Running}",
            resolvedPath, exists, alreadyRunning);

        return Task.FromResult(exists && !alreadyRunning);
    }

    public Task<bool> IsGameRunningAsync()
    {
        var running = _gameProcess != null && !_gameProcess.HasExited;
        if (!running && _gameProcess != null)
        {
            _gameProcess = null;
        }
        return Task.FromResult(running);
    }

    public Task StopGameAsync()
    {
        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            _gameProcess.Kill(entireProcessTree: true);
            _gameProcess.WaitForExit(5000);
            _logger.LogInformation("Game process terminated");
            Cleanup();
        }
        return Task.CompletedTask;
    }

    private static string GetLaunchArguments()
    {
        return "";
    }

    public async Task<List<LaunchCheckDto>> GetLaunchChecksAsync()
    {
        var checks = new List<LaunchCheckDto>();
        var now = DateTime.UtcNow;

        var hasCheat = await DetectCheatProcessesAsync();
        checks.Add(new LaunchCheckDto
        {
            Name = "Process Integrity",
            Status = hasCheat ? "failed" : "passed",
            Details = hasCheat
                ? "Suspicious process detected on system"
                : "No cheat processes detected",
        });

        var memAvailable = GetAvailableMemoryMB();
        checks.Add(new LaunchCheckDto
        {
            Name = "Memory Status",
            Status = memAvailable >= 1024 ? "passed" : "warning",
            Details = $"{memAvailable} MB available",
        });

        var gameExists = File.Exists(ResolveGamePath(null));
        checks.Add(new LaunchCheckDto
        {
            Name = "Game Installation",
            Status = gameExists ? "passed" : "failed",
            Details = gameExists ? "MTA:SA found" : "MTA:SA not found at default path",
        });

        var alreadyRunning = Process.GetProcessesByName(GameProcessName).Length > 0;
        checks.Add(new LaunchCheckDto
        {
            Name = "Already Running",
            Status = alreadyRunning ? "warning" : "passed",
            Details = alreadyRunning ? "MTA:SA is already running" : "No existing instance",
        });

        checks.Add(new LaunchCheckDto
        {
            Name = "Anti-Cheat Version",
            Status = "passed",
            Details = "v6.0.0",
        });

        return checks;
    }

    public Task<GameSettingsDto> GetSettingsAsync()
    {
        return Task.FromResult(_settings);
    }

    public Task UpdateSettingsAsync(GameSettingsDto settings)
    {
        _settings = settings;
        _logger.LogInformation("Game settings updated: Windowed={W}, SkipIntro={S}, DevConsole={D}",
            settings.WindowedMode, settings.SkipIntro, settings.DevConsole);
        return Task.CompletedTask;
    }

    public Task<GameStatusDto> GetStatusAsync()
    {
        var running = _gameProcess != null && !_gameProcess.HasExited;
        return Task.FromResult(new GameStatusDto
        {
            IsRunning = running,
            ProcessName = running ? GameExecutable : null,
            StartedAt = _startedAt,
            Uptime = running && _startedAt.HasValue ? DateTime.UtcNow - _startedAt.Value : null,
        });
    }

    public Task<string> GetGamePathAsync()
    {
        return Task.FromResult(ResolveGamePath(null));
    }

    private string ResolveGamePath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        var found = _pathFinder.GetExecutablePath();
        if (found != null)
            return found;

        _logger.LogInformation("Running full MTASA path search...");
        var mtasaRoot = _pathFinder.FindPath();
        if (mtasaRoot != null)
        {
            var exe = _pathFinder.GetExecutablePath(mtasaRoot);
            if (exe != null) return exe;
        }

        var commonPaths = new[]
        {
            DefaultGamePath,
            @"C:\Program Files (x86)\MTA San Andreas 1.6\MTA.exe",
            @"C:\Program Files\MTA San Andreas\MTA.exe",
            @"C:\Program Files (x86)\MTA San Andreas\MTA.exe",
        };

        return commonPaths.FirstOrDefault(File.Exists) ?? DefaultGamePath;
    }

    private async Task<bool> DetectCheatProcessesAsync()
    {
        var suspiciousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cheatengine", "cheat_engine", "artmoney", "tsearch",
            "wpe_pro", "injector", "extreme_injector", "processhacker",
        };

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (!suspiciousNames.Contains(proc.ProcessName))
                    continue;

                if (await _whitelist.IsProcessWhitelistedAsync(proc.ProcessName))
                    continue;

                return true;
            }
            catch
            {
            }
        }
        return false;
    }

    private static long GetAvailableMemoryMB()
    {
        try
        {
            var gcMemInfo = GC.GetGCMemoryInfo();
            return gcMemInfo.TotalAvailableMemoryBytes / 1024 / 1024;
        }
        catch
        {
            return 2048;
        }
    }

    private void StartNamedPipeServer()
    {
        _pipeCts?.Cancel();
        _pipeCts = new CancellationTokenSource();
        var token = _pipeCts.Token;

        _pipeListenerTask = Task.Run(async () =>
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("Named pipe server is only supported on Windows");
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        PipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(token);
                    _logger.LogInformation("Game client connected via named pipe");

                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };

                    while (!token.IsCancellationRequested && pipe.IsConnected)
                    {
                        var line = await reader.ReadLineAsync(token);
                        if (line == null) break;

                        _lastHeartbeat = DateTime.UtcNow;

                        if (line.StartsWith("HEARTBEAT", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("ACK");
                        }
                        else if (line.StartsWith("DETECTION", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', 3);
                            if (parts.Length >= 3)
                            {
                                _logger.LogWarning("In-game detection from MTA client: {Type} - {Desc}",
                                    parts[1], parts[2]);
                                await writer.WriteLineAsync("RECEIVED");
                            }
                        }
                        else if (line == "PING")
                        {
                            await writer.WriteLineAsync("PONG");
                        }
                        else
                        {
                            _logger.LogDebug("Unknown pipe message: {Msg}", line);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Named pipe connection ended");
                    await Task.Delay(1000, token);
                }
            }
        }, token);
    }

    private void StartProcessMonitor()
    {
        _monitorTask = Task.Run(async () =>
        {
            while (_gameProcess != null && !_gameProcess.HasExited)
            {
                await Task.Delay(5000);

                if (_lastHeartbeat.HasValue &&
                    (DateTime.UtcNow - _lastHeartbeat.Value).TotalMilliseconds > HeartbeatTimeoutMs)
                {
                    _logger.LogWarning("Game heartbeat timed out — no communication for {Timeout}ms", HeartbeatTimeoutMs);
                }
            }
        });
    }

    private void OnGameProcessExited(object? sender, EventArgs e)
    {
        var exitCode = _gameProcess?.ExitCode ?? -1;
        _logger.LogInformation("Game process exited with code {ExitCode}", exitCode);

        OnGameExit?.Invoke(this, new GameExitEventArgs
        {
            ExitCode = exitCode,
            ExitTime = DateTime.UtcNow,
        });

        Cleanup();
    }

    private void Cleanup()
    {
        _pipeCts?.Cancel();
        _gameProcess?.Dispose();
        _gameProcess = null;
        _startedAt = null;
        _lastHeartbeat = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
        _pipeCts?.Dispose();
    }
}
