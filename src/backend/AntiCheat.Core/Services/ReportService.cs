using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;
    private readonly AppDbContext _db;

    public ReportService(ILogger<ReportService> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<List<PlayerReportDto>> GetMyReportsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.PlayerReports
            .Where(r => r.ReporterId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PlayerReportDto
            {
                Id = r.Id,
                TicketType = r.TicketType,
                PlayerName = r.PlayerName,
                Reason = r.Reason,
                Description = r.Description,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Result = r.Result,
                ReporterId = r.ReporterId,
                ChatEnabled = r.ChatEnabled,
                IsFlagged = r.IsFlagged,
                AttachmentUrl = r.AttachmentUrl,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayerReportDto> SubmitReportAsync(string userId, ReportSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Data.Entities.PlayerReportEntity
        {
            TicketType = request.TicketType,
            PlayerName = request.PlayerName,
            Reason = request.Reason,
            Description = request.Description,
            Status = "pending",
            ReporterId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.PlayerReports.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Report submitted by {UserId} against {Player}", userId, request.PlayerName);
        return new PlayerReportDto
        {
            Id = entity.Id,
            TicketType = entity.TicketType,
            PlayerName = entity.PlayerName,
            Reason = entity.Reason,
            Description = entity.Description,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            Result = entity.Result,
            ReporterId = entity.ReporterId,
            ChatEnabled = entity.ChatEnabled,
            IsFlagged = entity.IsFlagged,
            AttachmentUrl = entity.AttachmentUrl,
        };
    }

    public async Task<PlayerReportDto?> GetReportByIdAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.PlayerReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (entity == null) return null;
        return new PlayerReportDto
        {
            Id = entity.Id,
            TicketType = entity.TicketType,
            PlayerName = entity.PlayerName,
            Reason = entity.Reason,
            Description = entity.Description,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            Result = entity.Result,
            ReporterId = entity.ReporterId,
            ChatEnabled = entity.ChatEnabled,
            IsFlagged = entity.IsFlagged,
            AttachmentUrl = entity.AttachmentUrl,
        };
    }

    public async Task<List<ReportMessageDto>> GetReportMessagesAsync(string reportId, CancellationToken cancellationToken = default)
    {
        return await _db.ReportMessages
            .Where(m => m.ReportId == reportId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ReportMessageDto
            {
                Id = m.Id,
                ReportId = m.ReportId,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                Message = m.Message,
                AttachmentUrl = m.AttachmentUrl,
                CreatedAt = m.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportMessageDto> SendReportMessageAsync(string reportId, string senderId, string senderName, string message, string? attachmentUrl = null, CancellationToken cancellationToken = default)
    {
        var msg = new Data.Entities.ReportMessageEntity
        {
            ReportId = reportId,
            SenderId = senderId,
            SenderName = senderName,
            Message = message,
            AttachmentUrl = attachmentUrl,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ReportMessages.Add(msg);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReportMessageDto
        {
            Id = msg.Id,
            ReportId = msg.ReportId,
            SenderId = msg.SenderId,
            SenderName = msg.SenderName,
            Message = msg.Message,
            AttachmentUrl = msg.AttachmentUrl,
            CreatedAt = msg.CreatedAt,
        };
    }

    public async Task<bool> ToggleReportChatAsync(string reportId, bool chatEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await _db.PlayerReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (entity == null) return false;
        entity.ChatEnabled = chatEnabled;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Report {ReportId} chat toggled to {ChatEnabled}", reportId, chatEnabled);
        return true;
    }

    public async Task<bool> UpdateReportStatusAsync(string reportId, string status, CancellationToken cancellationToken = default)
    {
        var entity = await _db.PlayerReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (entity == null) return false;
        entity.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Report {ReportId} status updated to {Status}", reportId, status);
        return true;
    }

    public string? GetReporterId(string reportId)
    {
        var entity = _db.PlayerReports.Find(reportId);
        return entity?.ReporterId;
    }
}
