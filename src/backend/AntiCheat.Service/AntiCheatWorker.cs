using System.Text.Json;
using AntiCheat.Core.Interfaces;
using AntiCheat.Service.Services;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Service;

public class AntiCheatWorker : BackgroundService
{
    private readonly IDetectionEngine _engine;
    private readonly ApiClientService _apiClient;
    private readonly ServiceScreenCapture _screenCapture;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AntiCheatWorker> _logger;
    private readonly IMtasaSerialReader _serialReader;
    private readonly IMtasaPathFinder _pathFinder;
    private bool _inPrelaunch;
    private DateTime _lastScreenshotTime = DateTime.MinValue;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScreenshotCooldown = TimeSpan.FromSeconds(10);
    private CancellationToken _stoppingToken;

    public AntiCheatWorker(
        IDetectionEngine engine,
        ApiClientService apiClient,
        ServiceScreenCapture screenCapture,
        IHostApplicationLifetime lifetime,
        ILogger<AntiCheatWorker> logger,
        IMtasaSerialReader serialReader,
        IMtasaPathFinder pathFinder)
    {
        _engine = engine;
        _apiClient = apiClient;
        _screenCapture = screenCapture;
        _lifetime = lifetime;
        _logger = logger;
        _serialReader = serialReader;
        _pathFinder = pathFinder;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _logger.LogInformation("Anti-Cheat background service started");

        _engine.OnDetection += async (_, detection) =>
        {
            if (!_inPrelaunch)
            {
                await ReportDetectionAsync(detection, "continuous", stoppingToken);

                if ((DateTime.UtcNow - _lastScreenshotTime) > ScreenshotCooldown)
                {
                    _lastScreenshotTime = DateTime.UtcNow;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _screenCapture.CaptureAndUploadAsync(
                                detectionEventId: detection.Id,
                                reason: $"{detection.Type}: {detection.Description}",
                                ct: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Auto-screenshot on detection failed");
                        }
                    }, stoppingToken);
                }
            }
        };

        try
        {
            _inPrelaunch = true;
            var preLaunchResults = await _engine.RunPreLaunchScanAsync(stoppingToken);
            _inPrelaunch = false;

            if (preLaunchResults.Count > 0)
            {
                _logger.LogWarning("Pre-launch scan found {Count} threats.", preLaunchResults.Count);
            }
            else
            {
                _logger.LogInformation("Pre-launch scan clean.");
            }

            await ReportDetectionsAsync(preLaunchResults.ToList(), "prelaunch", stoppingToken);

            var heartbeatTask = RunHeartbeatLoopAsync(stoppingToken);

            await _engine.RunContinuousScanAsync(stoppingToken);
            try { await heartbeatTask; } catch (OperationCanceledException) { }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Continuous scan loop terminated with error");
        }

        _logger.LogInformation("Anti-Cheat background service stopped");
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _engine.GetStatusAsync();
                var serialData = _serialReader.ReadSerial();
                var gamePath = _pathFinder.GetExecutablePath();

                var request = new ServiceHeartbeatRequest
                {
                    HardwareId = _apiClient.GetHardwareId(),
                    SerialNumber = serialData.Found ? serialData.Serial : null,
                    GamePath = gamePath,
                    Status = "scanning",
                    GameRunning = status != null,
                    DetectionCount = 0,
                    Timestamp = DateTime.UtcNow,
                };

                var response = await _apiClient.SendHeartbeatAsync(request, ct);
                if (response == null) continue;

                if (response.Commands.Contains("player_banned"))
                {
                    _logger.LogWarning("Player is banned — stopping anti-cheat service as requested");
                    _lifetime.StopApplication();
                    return;
                }

                if (response.Commands.Contains("run_prelaunch_scan"))
                {
                    _logger.LogInformation("API requested re-run of pre-launch scan");
                    var results = await _engine.RunPreLaunchScanAsync(ct);
                    await ReportDetectionsAsync(results.ToList(), "prelaunch", ct);
                }

                if (response.Commands.Contains("take_screenshot"))
                {
                    _logger.LogInformation("API requested screenshot capture");
                    var reason = response.CommandData.TryGetValue("screenshotRequestId", out var reqId)
                        ? $"manual-request:{reqId}" : "manual";
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _screenCapture.CaptureAndUploadAsync(reason: reason, ct: ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Requested screenshot capture failed");
                        }
                    }, ct);
                }

                if (response.Commands.Contains("start_stream"))
                {
                    if (response.CommandData.TryGetValue("streamSession", out var sessionJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(sessionJson);
                            var root = doc.RootElement;
                            var sessionId = root.GetProperty("SessionId").GetString() ?? "";
                            var targetFps = root.TryGetProperty("TargetFps", out var fpsProp) ? fpsProp.GetDouble() : 2.0;
                            var quality = root.TryGetProperty("JpegQuality", out var qProp) ? qProp.GetInt32() : 60;
                            await _screenCapture.StartStreaming(sessionId, targetFps, quality);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse stream start command data");
                        }
                    }
                }

                if (response.Commands.Contains("stop_stream"))
                {
                    await _screenCapture.StopStreaming();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Heartbeat loop iteration failed");
            }

            await Task.Delay(HeartbeatInterval, ct);
        }
    }

    private async Task ReportDetectionAsync(DetectionEventDto detection, string scanType, CancellationToken ct)
    {
        try
        {
            var report = new ServiceDetectionReport
            {
                HardwareId = _apiClient.GetHardwareId(),
                ScanType = scanType,
                Detections = new List<DetectionEventDto> { detection },
                ReportedAt = DateTime.UtcNow,
            };
            await _apiClient.ReportDetectionsAsync(report, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report detection to API");
        }
    }

    private async Task ReportDetectionsAsync(IReadOnlyList<DetectionEventDto> detections, string scanType, CancellationToken ct)
    {
        if (detections.Count == 0 && scanType != "prelaunch")
            return;

        try
        {
            var report = new ServiceDetectionReport
            {
                HardwareId = _apiClient.GetHardwareId(),
                ScanType = scanType,
                Detections = detections.ToList(),
                ReportedAt = DateTime.UtcNow,
            };
            await _apiClient.ReportDetectionsAsync(report, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report {Count} detections to API", detections.Count);
        }
    }
}
