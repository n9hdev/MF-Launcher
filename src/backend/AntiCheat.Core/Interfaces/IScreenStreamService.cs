using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IScreenStreamService
{
    Task<ScreenStreamSessionDto> CreateSessionAsync(string playerId, string? linkedDetectionId = null);
    Task<ScreenStreamSessionDto?> GetSessionAsync(string sessionId);
    Task<List<StreamSummaryDto>> GetActiveSessionsAsync();
    Task<List<StreamSummaryDto>> GetHistoryAsync(string playerId, int limit = 20);
    Task<bool> EndSessionAsync(string sessionId);
    Task<ScreenStreamSessionDto> AddViewerAsync(string sessionId, string adminId, string adminName, string connectionId);
    Task<bool> RemoveViewerAsync(string sessionId, string adminId);
    Task<bool> UpdateFpsAsync(string sessionId, double targetFps);
    Task<int> GetViewerCountAsync(string sessionId);
    Task<int> IncrementFrameCountAsync(string sessionId);
    bool CanAcceptMoreViewers(string sessionId);
    Task CleanupStaleSessionsAsync();
}
