using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IRemoteApiService
{
    Task<HeartbeatResponse?> SendHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<GlobalVerifyResponse?> VerifyAsync(GlobalVerifyRequest request, CancellationToken cancellationToken = default);
    Task<bool> ReportAsync(AntiReportRequest request, CancellationToken cancellationToken = default);
    Task<UpdateCheckResponse?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);
    Task<PlayerProfileResponse?> GetProfileAsync(string hwid, CancellationToken cancellationToken = default);
}
