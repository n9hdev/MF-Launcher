using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IReportService
{
    Task<List<PlayerReportDto>> GetMyReportsAsync(string userId, CancellationToken cancellationToken = default);
    Task<PlayerReportDto> SubmitReportAsync(string userId, ReportSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<PlayerReportDto?> GetReportByIdAsync(string reportId, CancellationToken cancellationToken = default);
    Task<List<ReportMessageDto>> GetReportMessagesAsync(string reportId, CancellationToken cancellationToken = default);
    Task<ReportMessageDto> SendReportMessageAsync(string reportId, string senderId, string senderName, string message, string? attachmentUrl = null, CancellationToken cancellationToken = default);
    Task<bool> ToggleReportChatAsync(string reportId, bool chatEnabled, CancellationToken cancellationToken = default);
    Task<bool> UpdateReportStatusAsync(string reportId, string status, CancellationToken cancellationToken = default);
    string? GetReporterId(string reportId);
}
