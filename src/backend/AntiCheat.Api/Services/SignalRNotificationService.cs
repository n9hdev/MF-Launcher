using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AntiCheat.Api.Hubs;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Api.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<AntiCheatHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(IHubContext<AntiCheatHub> hubContext, ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyBanAsync(string playerId, object banInfo, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"user_{playerId}").SendAsync("BanStatus", banInfo, ct);
        _logger.LogInformation("Sent BanStatus notification to user {PlayerId}", playerId);
    }

    public async Task NotifyTrustStatusChangedAsync(string playerId, string trustStatus, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"user_{playerId}").SendAsync("TrustStatusChanged", trustStatus, ct);
        _logger.LogInformation("Sent TrustStatusChanged notification to user {PlayerId}: {Status}", playerId, trustStatus);
    }

    public async Task NotifyHwidVerifiedAsync(string playerId, bool verified, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"user_{playerId}").SendAsync("HwidVerified", new { verified }, ct);
        _logger.LogInformation("Sent HwidVerified notification to user {PlayerId}: {Verified}", playerId, verified);
    }
}
