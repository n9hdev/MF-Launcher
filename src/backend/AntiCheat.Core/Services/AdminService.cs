using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Core.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var bans = await _db.BanEntries.ToListAsync();
        var appeals = await _db.Appeals.ToListAsync();
        var activeBans = bans.Count(b => b.Active);
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentDetections = await _db.DetectionEvents
            .Where(d => d.Timestamp >= thirtyDaysAgo)
            .CountAsync();
        var recentThreats = await _db.DetectionEvents
            .Where(d => d.Timestamp >= thirtyDaysAgo && (d.Severity == "high" || d.Severity == "critical"))
            .CountAsync();
        var detectionRate = recentDetections > 0
            ? Math.Round((double)recentThreats / recentDetections * 100, 1)
            : 0;
        return new AdminStatsDto
        {
            ActiveBans = activeBans,
            PermanentBans = bans.Count(b => b.Type == "Permanent" && b.Active),
            TemporaryBans = bans.Count(b => b.Type == "Temporary" && b.Active),
            PendingAppeals = appeals.Count(a => a.Status == "Pending"),
            DetectionRate = detectionRate,
            TotalPlayers = await _db.Users.CountAsync(),
        };
    }

    public async Task<List<DetectorPerformanceDto>> GetDetectorPerformanceAsync()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var groups = await _db.DetectionEvents
            .Where(d => d.Timestamp >= thirtyDaysAgo)
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var threats = await _db.DetectionEvents
            .Where(d => d.Timestamp >= thirtyDaysAgo && (d.Severity == "high" || d.Severity == "critical"))
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var knownDetectors = new[] { "Memory Scanner", "Process Analyzer", "Injection Detector", "Kernel Scanner", "YARA Scanner", "Network Monitor" };
        return knownDetectors.Select(name =>
        {
            var total = groups.FirstOrDefault(g => g.Type == name)?.Count ?? 0;
            var threatCount = threats.FirstOrDefault(g => g.Type == name)?.Count ?? 0;
            var accuracy = total > 0 ? (int)Math.Round((double)threatCount / total * 100) : 0;
            var status = total == 0 ? "inactive" : accuracy >= 80 ? "active" : "warning";
            return new DetectorPerformanceDto
            {
                Name = name,
                Detections = total,
                Accuracy = accuracy,
                Status = status,
            };
        }).ToList();
    }

    public async Task<List<AdminBanEntryDto>> GetBansAsync()
    {
        return await _db.BanEntries
            .OrderByDescending(b => b.IssuedAt)
            .Select(b => new AdminBanEntryDto
            {
                Id = b.Id,
                Player = b.Player,
                PlayerId = b.PlayerId,
                Reason = b.Reason,
                Type = b.Type,
                IssuedBy = b.IssuedBy,
                IssuedAt = b.IssuedAt,
                Active = b.Active,
                Appeals = b.Appeals,
                SerialNumber = b.SerialNumber,
                IpAddress = b.IpAddress,
                ProofUrl = b.ProofUrl,
                BannedAt = b.BannedAt,
                DurationHours = b.DurationHours,
            })
            .ToListAsync();
    }

    public async Task<AdminBanEntryDto> GetBanByIdAsync(string id)
    {
        var entity = await _db.BanEntries.FindAsync(id);
        if (entity == null) return null;
        return new AdminBanEntryDto
        {
            Id = entity.Id,
            Player = entity.Player,
            PlayerId = entity.PlayerId,
            Reason = entity.Reason,
            Type = entity.Type,
            IssuedBy = entity.IssuedBy,
            IssuedAt = entity.IssuedAt,
            Active = entity.Active,
            Appeals = entity.Appeals,
            SerialNumber = entity.SerialNumber,
            IpAddress = entity.IpAddress,
            ProofUrl = entity.ProofUrl,
            BannedAt = entity.BannedAt,
            DurationHours = entity.DurationHours,
        };
    }

    public async Task<AdminBanEntryDto> CreateBanAsync(CreateBanRequest request)
    {
        var entity = new Data.Entities.BanEntryEntity
        {
            Player = request.Player,
            Reason = request.Reason,
            Type = request.Type,
            IssuedBy = request.IssuedBy,
            IssuedAt = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Active = true,
            Appeals = 0,
        };
        _db.BanEntries.Add(entity);
        await _db.SaveChangesAsync();
        return new AdminBanEntryDto
        {
            Id = entity.Id,
            Player = entity.Player,
            PlayerId = entity.PlayerId,
            Reason = entity.Reason,
            Type = entity.Type,
            IssuedBy = entity.IssuedBy,
            IssuedAt = entity.IssuedAt,
            Active = entity.Active,
            Appeals = entity.Appeals,
            SerialNumber = entity.SerialNumber,
            IpAddress = entity.IpAddress,
            ProofUrl = entity.ProofUrl,
            BannedAt = entity.BannedAt,
            DurationHours = entity.DurationHours,
        };
    }

    public async Task<AdminBanEntryDto> UpdateBanAsync(string id, UpdateBanRequest request)
    {
        var entity = await _db.BanEntries.FindAsync(id);
        if (entity != null)
        {
            entity.Reason = request.Reason;
            entity.Type = request.Type;
            entity.Active = request.Active;
            await _db.SaveChangesAsync();
        }
        return await GetBanByIdAsync(id);
    }

    public async Task<bool> RevokeBanAsync(string id)
    {
        var entity = await _db.BanEntries.FindAsync(id);
        if (entity == null) return false;
        entity.Active = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<AdminAppealDto>> GetAppealsAsync()
    {
        return await _db.Appeals
            .OrderByDescending(a => a.Date)
            .Select(a => new AdminAppealDto
            {
                Id = a.Id,
                Player = a.Player,
                Reason = a.Reason,
                BanType = a.BanType,
                Status = a.Status,
                Date = a.Date,
                Reviewer = a.Reviewer,
            })
            .ToListAsync();
    }

    public async Task<AdminAppealDto> GetAppealByIdAsync(string id)
    {
        var entity = await _db.Appeals
            .Include(a => a.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null) return null;
        return MapToAppealDto(entity);
    }

    public async Task<AdminAppealDto> UpdateAppealStatusAsync(string id, string status, string reviewer)
    {
        var entity = await _db.Appeals
            .Include(a => a.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(a => a.Id == id);
        if (entity != null)
        {
            entity.Status = status;
            entity.Reviewer = reviewer;
            await _db.SaveChangesAsync();
        }
        return MapToAppealDto(entity);
    }

    public async Task<List<AppealMessageDto>> GetAppealMessagesAsync(string appealId)
    {
        var msgs = await _db.AppealMessages
            .Where(m => m.AppealId == appealId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return msgs.Select(m => new AppealMessageDto
        {
            Id = m.Id,
            AppealId = m.AppealId,
            SenderId = m.SenderId,
            SenderName = m.SenderName,
            Message = m.Message,
            CreatedAt = m.CreatedAt,
        }).ToList();
    }

    public async Task<AppealMessageDto> SendAppealReplyAsync(string appealId, string senderId, string senderName, string message)
    {
        var msg = new Data.Entities.AppealMessageEntity
        {
            Id = Guid.NewGuid().ToString(),
            AppealId = appealId,
            SenderId = senderId,
            SenderName = senderName,
            Message = message,
            CreatedAt = DateTime.UtcNow,
        };
        _db.AppealMessages.Add(msg);
        await _db.SaveChangesAsync();
        return new AppealMessageDto
        {
            Id = msg.Id,
            AppealId = msg.AppealId,
            SenderId = msg.SenderId,
            SenderName = msg.SenderName,
            Message = msg.Message,
            CreatedAt = msg.CreatedAt,
        };
    }

    private static AdminAppealDto MapToAppealDto(AppealEntity entity)
    {
        return new AdminAppealDto
        {
            Id = entity.Id,
            Player = entity.Player,
            PlayerId = entity.PlayerId,
            BanId = entity.BanId,
            Reason = entity.Reason,
            BanType = entity.BanType,
            Status = entity.Status,
            Date = entity.Date,
            Reviewer = entity.Reviewer,
            Messages = entity.Messages?.Select(m => new AppealMessageDto
            {
                Id = m.Id,
                AppealId = m.AppealId,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                Message = m.Message,
                CreatedAt = m.CreatedAt,
            }).ToList() ?? new(),
        };
    }

    public async Task<List<WhitelistEntryDto>> GetWhitelistAsync()
    {
        return await _db.WhitelistEntries
            .Select(w => new WhitelistEntryDto
            {
                Id = w.Id,
                Entry = w.Entry,
                Type = w.Type,
                AddedBy = w.AddedBy,
                AddedAt = w.AddedAt,
                Reason = w.Reason,
            })
            .ToListAsync();
    }

    public async Task<WhitelistEntryDto> AddWhitelistEntryAsync(AddWhitelistEntryRequest request)
    {
        var entity = new Data.Entities.WhitelistEntryEntity
        {
            Entry = request.Entry,
            Type = request.Type,
            AddedBy = request.AddedBy,
            AddedAt = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Reason = request.Reason,
        };
        _db.WhitelistEntries.Add(entity);
        await _db.SaveChangesAsync();
        return new WhitelistEntryDto
        {
            Id = entity.Id,
            Entry = entity.Entry,
            Type = entity.Type,
            AddedBy = entity.AddedBy,
            AddedAt = entity.AddedAt,
            Reason = entity.Reason,
        };
    }

    public async Task<bool> RemoveWhitelistEntryAsync(string id)
    {
        var entity = await _db.WhitelistEntries.FindAsync(id);
        if (entity == null) return false;
        _db.WhitelistEntries.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<WhitelistEntryDto> UpdateWhitelistEntryAsync(string id, UpdateWhitelistEntryRequest request)
    {
        var entity = await _db.WhitelistEntries.FindAsync(id);
        if (entity != null)
        {
            entity.Entry = request.Entry;
            entity.Type = request.Type;
            entity.Reason = request.Reason;
            await _db.SaveChangesAsync();
        }
        return await GetWhitelistAsync().ContinueWith(t => t.Result.FirstOrDefault(w => w.Id == id));
    }

    public async Task<List<WeeklyActivityDto>> GetWeeklyActivityAsync()
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var detections = await _db.DetectionEvents
            .Where(d => d.Timestamp >= sevenDaysAgo)
            .ToListAsync();
        var sessions = await _db.Sessions
            .Where(s => s.CreatedAt >= sevenDaysAgo)
            .ToListAsync();

        var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        return dayNames.Select((name, i) =>
        {
            var dayStart = DateTime.UtcNow.AddDays(-(6 - i)).Date;
            var dayEnd = dayStart.AddDays(1);
            return new WeeklyActivityDto
            {
                Day = name,
                Scans = detections.Count(d => d.Timestamp >= dayStart && d.Timestamp < dayEnd),
                Threats = detections.Count(d => d.Timestamp >= dayStart && d.Timestamp < dayEnd && d.Severity is "high" or "critical"),
                Players = sessions.Where(s => s.CreatedAt >= dayStart && s.CreatedAt < dayEnd).Select(s => s.UserId).Distinct().Count(),
            };
        }).ToList();
    }

    public async Task<List<ThreatDistributionDto>> GetThreatDistributionAsync()
    {
        var groups = await _db.DetectionEvents
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var total = groups.Sum(g => g.Count);
        return groups.Select(g => new ThreatDistributionDto
        {
            Type = g.Type,
            Count = g.Count,
            Pct = total > 0 ? Math.Round((double)g.Count / total * 100, 1) : 0,
        }).ToList();
    }

    public async Task<List<TopReportDto>> GetTopReportsAsync()
    {
        var top = await _db.PlayerReports
            .GroupBy(r => r.PlayerName)
            .Select(g => new { Player = g.Key, Reports = g.Count() })
            .OrderByDescending(x => x.Reports)
            .Take(5)
            .ToListAsync();

        var result = new List<TopReportDto>();
        foreach (var t in top)
        {
            var hasBan = await _db.BanEntries.AnyAsync(b => b.Player == t.Player && b.Active);
            result.Add(new TopReportDto
            {
                Player = t.Player,
                Reports = t.Reports,
                Action = hasBan ? "Banned" : "Investigated",
            });
        }
        return result;
    }
}
