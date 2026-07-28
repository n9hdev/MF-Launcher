using AntiCheat.Core.Configuration;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Options;

namespace AntiCheat.Api.Services;

public class HeartbeatService : BackgroundService
{
    private readonly IRemoteApiService _remoteApi;
    private readonly RemoteApiSettings _settings;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly IGameLauncher _gameLauncher;
    private readonly IHardwareIdProvider _hwidProvider;
    private readonly IMtasaSerialReader _serialReader;

    public HeartbeatService(
        IRemoteApiService remoteApi,
        IOptions<RemoteApiSettings> settings,
        ILogger<HeartbeatService> logger,
        IGameLauncher gameLauncher,
        IHardwareIdProvider hwidProvider,
        IMtasaSerialReader serialReader)
    {
        _remoteApi = remoteApi;
        _settings = settings.Value;
        _logger = logger;
        _gameLauncher = gameLauncher;
        _hwidProvider = hwidProvider;
        _serialReader = serialReader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Remote API heartbeat is disabled");
            return;
        }

        _logger.LogInformation("Remote API heartbeat service started (interval: {Interval}s)",
            _settings.HeartbeatIntervalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await SendHeartbeatAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_settings.HeartbeatIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Heartbeat service terminated with error");
        }

        _logger.LogInformation("Remote API heartbeat service stopped");
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            var gameRunning = await _gameLauncher.IsGameRunningAsync();
            var serialData = _serialReader.ReadSerial();

            var request = new HeartbeatRequest
            {
                Hwid = _hwidProvider.GetHardwareId(),
                Status = "online",
                Timestamp = DateTime.UtcNow,
                GameRunning = gameRunning,
                Serial = serialData.Found ? serialData.Serial : null,
            };

            await _remoteApi.SendHeartbeatAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Heartbeat send failed");
        }
    }
}
