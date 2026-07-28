using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ActivityService : IActivityService
{
    private readonly ILogger<ActivityService> _logger;
    private readonly AppDbContext _db;

    public ActivityService(ILogger<ActivityService> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<List<ActivityEventDto>> GetRecentActivityAsync(string userId, int count = 10, CancellationToken cancellationToken = default)
    {
        var items = await _db.DetectionEvents
            .Where(e => e.PlayerId == userId)
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);

        return items.Select(e =>
        {
            string icon = e.Severity switch
            {
                "critical" => "alert",
                "high" => "alert",
                "medium" => "shield",
                _ => "activity",
            };
            return new ActivityEventDto
            {
                Id = e.Id,
                Type = e.Type,
                Title = e.Description.Length > 80 ? e.Description.Substring(0, 80) : e.Description,
                Description = e.Description,
                Timestamp = e.Timestamp,
                Severity = e.Severity,
                Icon = icon,
            };
        }).ToList();
    }
}
