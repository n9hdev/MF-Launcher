using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string user, string target = "", string details = "", string ip = "");
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(string action, string user, string target = "", string details = "", string ip = "")
    {
        try
        {
            var entry = new AuditLogEntryEntity
            {
                Id = Guid.NewGuid().ToString(),
                Action = action,
                User = user,
                Target = target,
                Details = details,
                Timestamp = DateTime.UtcNow.ToString("O"),
                Ip = ip
            };

            _db.AuditLogEntries.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log: action={Action} user={User} target={Target}", action, user, target);
        }
    }
}
