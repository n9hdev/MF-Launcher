using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IHistoryService
{
    Task<List<TimelineEventDto>> GetTimelineAsync(string userId, string? severity = null, string? category = null, string? search = null, int page = 1, int limit = 20, CancellationToken cancellationToken = default);
    Task<HistorySummaryDto> GetSummaryAsync(string userId, CancellationToken cancellationToken = default);
    Task<DetectionStatsDto> GetStatsAsync(string userId, CancellationToken cancellationToken = default);
}
