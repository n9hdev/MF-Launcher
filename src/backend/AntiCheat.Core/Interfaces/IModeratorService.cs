using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IModeratorService
{
    Task<ModeratorStatsDto> GetStatsAsync();
    Task<List<ActiveAlertDto>> GetActiveAlertsAsync();
    Task<List<AlertDto>> GetAlertsAsync();
    Task ResolveAlertAsync(string alertId);
    Task<List<PlayerSearchResultDto>> SearchPlayersAsync(string? query, string? email = null, string? status = null, int? minReports = null, int? maxReports = null);
    Task<PlayerDetailDto> GetPlayerDetailAsync(string id);
    Task<List<PlayerReportDto>> GetPlayerReportsAsync(string playerId);
}
