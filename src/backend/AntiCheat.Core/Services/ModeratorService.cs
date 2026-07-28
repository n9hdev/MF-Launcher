using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ModeratorService : IModeratorService
{
    private readonly ILogger<ModeratorService> _logger;
    private readonly AppDbContext _db;

    public ModeratorService(ILogger<ModeratorService> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<ModeratorStatsDto> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        var openReports = await _db.PlayerReports.CountAsync(r => r.Status == "pending" || r.Status == "investigating");
        var activePlayers = await _db.Users.CountAsync(u => u.Status == "online");
        var unresolvedAlerts = await _db.Alerts.CountAsync(a => !a.Resolved);
        var resolvedToday = await _db.PlayerReports.CountAsync(r =>
            (r.Status == "resolved" || r.Status == "dismissed") && r.CreatedAt >= todayStart);

        var totalReports = await _db.PlayerReports.CountAsync();
        var firstReport = await _db.PlayerReports
            .OrderBy(r => r.CreatedAt)
            .Select(r => (DateTime?)r.CreatedAt)
            .FirstOrDefaultAsync();
        var hoursElapsed = firstReport.HasValue
            ? Math.Max(1, (now - firstReport.Value).TotalHours)
            : 1;
        var reportsPerHour = Math.Round(totalReports / hoursElapsed, 1);

        var totalBans = await _db.BanEntries.CountAsync();
        var totalDetections = await _db.DetectionEvents.CountAsync();
        var banAccuracy = totalDetections > 0
            ? Math.Round((double)totalBans / totalDetections * 100, 1)
            : 0;

        return new ModeratorStatsDto
        {
            OpenReports = openReports,
            ActivePlayers = activePlayers,
            ActiveAlerts = unresolvedAlerts,
            ResolvedToday = resolvedToday,
            AvgResponseTime = 0,
            ReportsPerHour = reportsPerHour,
            BanAccuracy = banAccuracy,
        };
    }

    public async Task<List<ActiveAlertDto>> GetActiveAlertsAsync()
    {
        return await _db.Alerts
            .Where(a => !a.Resolved)
            .Select(a => new ActiveAlertDto
            {
                Type = a.Title,
                Severity = a.Severity,
                PlayerName = a.ProcessName,
                TimeAgo = a.Timestamp,
            })
            .ToListAsync();
    }

    public async Task<List<AlertDto>> GetAlertsAsync()
    {
        return await _db.Alerts
            .Select(a => new AlertDto
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                Severity = a.Severity,
                Confidence = a.Confidence,
                Timestamp = a.Timestamp,
                ProcessName = a.ProcessName,
                Resolved = a.Resolved,
            })
            .ToListAsync();
    }

    public async Task ResolveAlertAsync(string alertId)
    {
        var alert = await _db.Alerts.FindAsync(alertId);
        if (alert != null)
        {
            alert.Resolved = true;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Alert resolved: {AlertId}", alertId);
        }
    }

    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(string? query, string? email = null, string? status = null, int? minReports = null, int? maxReports = null)
    {
        var users = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            users = users.Where(u => u.Username.Contains(query));
        if (!string.IsNullOrWhiteSpace(email))
            users = users.Where(u => u.Email != null && u.Email.Contains(email));
        if (!string.IsNullOrWhiteSpace(status))
            users = users.Where(u => u.Status == status);

        var results = await users.ToListAsync();

        var filtered = results.Select(u => new
        {
            User = u,
            ReportsCount = _db.PlayerReports.Count(r => r.ReporterId == u.Id),
            BansCount = _db.BanEntries.Count(b => b.PlayerId == u.Id),
        });

        if (minReports.HasValue)
            filtered = filtered.Where(x => x.ReportsCount >= minReports.Value);
        if (maxReports.HasValue)
            filtered = filtered.Where(x => x.ReportsCount <= maxReports.Value);

        return filtered.Select(x => new PlayerSearchResultDto
        {
            Id = x.User.Id,
            Username = x.User.Username,
            Email = x.User.Email,
            TrustScore = x.User.TrustScore,
            Status = x.User.Status,
            LastSeen = x.User.LastLoginAt?.ToString("g") ?? "Never",
            GameName = x.User.GamePath ?? "MTA:SA",
            HoursPlayed = _db.Sessions.Count(s => s.UserId == x.User.Id),
            ReportsCount = x.ReportsCount,
            BansCount = x.BansCount,
        }).ToList();
    }

    public async Task<PlayerDetailDto> GetPlayerDetailAsync(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        var sessions = await _db.Sessions
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionSummaryDto
            {
                Id = s.SessionId,
                IpAddress = s.IpAddress,
                DeviceId = s.DeviceId,
                CreatedAt = s.CreatedAt.ToString("g"),
                LastActivity = s.LastActivity != null ? s.LastActivity.Value.ToString("g") : "",
                IsActive = s.IsActive,
            })
            .ToListAsync();

        var detections = await _db.DetectionEvents
            .Where(d => d.PlayerId == id)
            .OrderByDescending(d => d.Timestamp)
            .Select(d => new DetectionEventDto
            {
                Id = d.Id,
                Type = d.Type,
                Severity = d.Severity,
                Timestamp = d.Timestamp,
                Description = d.Description,
                Confidence = d.Confidence,
                PlayerId = d.PlayerId,
                ProcessName = d.ProcessName,
            })
            .ToListAsync();

        return new PlayerDetailDto
        {
            Id = user.Id,
            Username = user.Username,
            IpAddress = sessions.FirstOrDefault()?.IpAddress,
            HardwareId = user.HardwareId,
            SerialNumber = user.SerialNumber,
            Status = user.Status,
            GameName = user.GamePath ?? "MTA:SA",
            TrustScore = user.TrustScore,
            HoursPlayed = sessions.Count,
            ReportsCount = await _db.PlayerReports.CountAsync(r => r.ReporterId == id),
            BansCount = await _db.BanEntries.CountAsync(b => b.PlayerId == id),
            LastSeen = user.LastLoginAt?.ToString("g") ?? "Never",
            Sessions = sessions,
            Detections = detections,
        };
    }

    public async Task<List<PlayerReportDto>> GetPlayerReportsAsync(string playerId)
    {
        return await _db.PlayerReports
            .Where(r => r.ReporterId == playerId || r.PlayerName == playerId)
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
            .ToListAsync();
    }
}
