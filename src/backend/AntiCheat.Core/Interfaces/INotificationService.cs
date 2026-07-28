namespace AntiCheat.Core.Interfaces;

public interface INotificationService
{
    Task NotifyBanAsync(string playerId, object banInfo, CancellationToken ct = default);
    Task NotifyTrustStatusChangedAsync(string playerId, string trustStatus, CancellationToken ct = default);
    Task NotifyHwidVerifiedAsync(string playerId, bool verified, CancellationToken ct = default);
}
