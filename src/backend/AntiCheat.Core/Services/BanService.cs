using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;

namespace AntiCheat.Core.Services;

public class BanService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BanService> _logger;

    public BanService(AppDbContext db, ILogger<BanService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BanEntryEntity> AutoBanAsync(
        string playerId, string playerName, string reason, string issuedBy,
        string? serial = null, string? ip = null, string? proofUrl = null,
        string? banType = null, CancellationToken ct = default)
    {
        // Atomic check: prevent duplicate bans even under concurrent requests
        var existing = await _db.BanEntries
            .Where(b => b.PlayerId == playerId && b.Active)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            _logger.LogWarning("Auto-ban skipped: {Player} ({Id}) already has active ban since {Since}",
                playerName, playerId, existing.BannedAt);
            return existing;
        }

        banType ??= "Permanent";
        var ban = new BanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            Player = playerName,
            Reason = reason,
            Type = banType,
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Active = true,
            Appeals = 0,
            PlayerId = playerId,
            SerialNumber = serial ?? "",
            IpAddress = ip ?? "",
            ProofUrl = proofUrl ?? "",
            BannedAt = DateTime.UtcNow,
            DurationHours = banType == "Permanent" ? -1 : 0,
        };

        _db.BanEntries.Add(ban);

        var user = await _db.Users.FindAsync(new object[] { playerId }, ct);
        if (user != null)
        {
            user.Status = "banned";
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogWarning("Auto-ban: {Player} ({Id}) - {Reason}", playerName, playerId, reason);
        return ban;
    }

    public async Task<bool> IsPlayerBannedAsync(string playerId, CancellationToken ct = default)
    {
        return await _db.BanEntries.AnyAsync(b =>
            b.PlayerId == playerId && b.Active, ct);
    }

    public async Task<BanEntryEntity?> GetActiveBanAsync(string playerId, CancellationToken ct = default)
    {
        return await _db.BanEntries
            .Where(b => b.PlayerId == playerId && b.Active)
            .OrderByDescending(b => b.BannedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<BanEntryEntity>> GetPlayerBansAsync(string playerId, CancellationToken ct = default)
    {
        return await _db.BanEntries
            .Where(b => b.PlayerId == playerId)
            .OrderByDescending(b => b.BannedAt)
            .ToListAsync(ct);
    }

    public async Task<AppealEntity> SubmitAppealAsync(string banId, string playerId, string playerName, string message, CancellationToken ct = default)
    {
        var ban = await _db.BanEntries.FindAsync(new object[] { banId }, ct);
        if (ban == null)
            throw new KeyNotFoundException("Ban not found");

        var existingAppeal = await _db.Appeals
            .FirstOrDefaultAsync(a => a.BanId == banId && a.Status == "Pending", ct);

        AppealEntity appeal;
        if (existingAppeal != null)
        {
            appeal = existingAppeal;
        }
        else
        {
            appeal = new AppealEntity
            {
                Id = Guid.NewGuid().ToString(),
                Player = playerName,
                PlayerId = playerId,
                BanId = banId,
                Reason = message,
                BanType = ban.Type,
                Status = "Pending",
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Reviewer = "\u2014",
            };
            _db.Appeals.Add(appeal);
        }

        var appealMsg = new AppealMessageEntity
        {
            Id = Guid.NewGuid().ToString(),
            AppealId = appeal.Id,
            SenderId = playerId,
            SenderName = playerName,
            Message = message,
            CreatedAt = DateTime.UtcNow,
        };
        _db.AppealMessages.Add(appealMsg);

        ban.Appeals = await _db.Appeals.CountAsync(a => a.Player == playerName, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Appeal submitted for ban {BanId} by {Player}", banId, playerName);
        return appeal;
    }

    public async Task<List<AppealMessageEntity>> GetAppealMessagesAsync(string appealId, CancellationToken ct = default)
    {
        return await _db.AppealMessages
            .Where(m => m.AppealId == appealId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<AppealMessageEntity> SendAppealMessageAsync(string appealId, string senderId, string senderName, string message, CancellationToken ct = default)
    {
        var msg = new AppealMessageEntity
        {
            Id = Guid.NewGuid().ToString(),
            AppealId = appealId,
            SenderId = senderId,
            SenderName = senderName,
            Message = message,
            CreatedAt = DateTime.UtcNow,
        };
        _db.AppealMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        return msg;
    }

    public async Task<AppealEntity?> GetPlayerAppealAsync(string playerId, CancellationToken ct = default)
    {
        return await _db.Appeals
            .Where(a => a.PlayerId == playerId)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(ct);
    }
}
