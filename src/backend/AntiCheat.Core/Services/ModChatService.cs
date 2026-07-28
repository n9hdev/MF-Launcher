using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ModChatService : IModChatService
{
    private readonly ILogger<ModChatService> _logger;
    private readonly AppDbContext _db;

    public ModChatService(ILogger<ModChatService> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<List<ModChatMessageDto>> GetMessagesAsync()
    {
        return await _db.ModChatMessages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ModChatMessageDto
            {
                Id = m.Id,
                UserId = m.UserId,
                User = m.Username,
                Message = m.Message,
                AttachmentUrl = m.AttachmentUrl,
                Role = m.Role,
                CreatedAt = m.CreatedAt,
                TimeAgo = FormatTimeAgo(m.CreatedAt),
            })
            .ToListAsync();
    }

    public async Task<List<ModeratorOnlineDto>> GetOnlineModeratorsAsync()
    {
        return await _db.Users
            .Where(u => (u.Role == "moderator" || u.Role == "admin" || u.Role == "superadmin") && u.Status == "online")
            .Select(u => new ModeratorOnlineDto
            {
                Name = u.Username,
                Status = u.Status,
            })
            .ToListAsync();
    }

    public async Task<ModChatMessageDto> SendMessageAsync(string userId, string username, string message, string? attachmentUrl = null, string role = "moderator")
    {
        var entity = new ModChatMessageEntity
        {
            UserId = userId,
            Username = username,
            Message = message,
            AttachmentUrl = attachmentUrl,
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ModChatMessages.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("ModChat: {User} sent a message", username);

        return new ModChatMessageDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            User = entity.Username,
            Message = entity.Message,
            AttachmentUrl = entity.AttachmentUrl,
            Role = entity.Role,
            CreatedAt = entity.CreatedAt,
            TimeAgo = "just now",
        };
    }

    private static string FormatTimeAgo(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}
