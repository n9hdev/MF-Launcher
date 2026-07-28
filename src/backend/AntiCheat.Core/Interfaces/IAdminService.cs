using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IAdminService
{
    Task<AdminStatsDto> GetStatsAsync();
    Task<List<DetectorPerformanceDto>> GetDetectorPerformanceAsync();
    Task<List<AdminBanEntryDto>> GetBansAsync();
    Task<AdminBanEntryDto> GetBanByIdAsync(string id);
    Task<AdminBanEntryDto> CreateBanAsync(CreateBanRequest request);
    Task<AdminBanEntryDto> UpdateBanAsync(string id, UpdateBanRequest request);
    Task<bool> RevokeBanAsync(string id);
    Task<List<AdminAppealDto>> GetAppealsAsync();
    Task<AdminAppealDto> GetAppealByIdAsync(string id);
    Task<AdminAppealDto> UpdateAppealStatusAsync(string id, string status, string reviewer);
    Task<List<AppealMessageDto>> GetAppealMessagesAsync(string appealId);
    Task<AppealMessageDto> SendAppealReplyAsync(string appealId, string senderId, string senderName, string message);
    Task<List<WhitelistEntryDto>> GetWhitelistAsync();
    Task<WhitelistEntryDto> AddWhitelistEntryAsync(AddWhitelistEntryRequest request);
    Task<bool> RemoveWhitelistEntryAsync(string id);
    Task<WhitelistEntryDto> UpdateWhitelistEntryAsync(string id, UpdateWhitelistEntryRequest request);
    Task<List<WeeklyActivityDto>> GetWeeklyActivityAsync();
    Task<List<ThreatDistributionDto>> GetThreatDistributionAsync();
    Task<List<TopReportDto>> GetTopReportsAsync();
}
