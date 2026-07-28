using System.Collections.Concurrent;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class DedupService : IDedupService
{
    private readonly ConcurrentDictionary<string, CachedFingerprint> _cache = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DedupService> _logger;
    private readonly int _windowMinutes;
    private readonly bool _enabled;

    public DedupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DedupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = configuration.GetValue<bool>("DetectionDeduplication:Enabled", true);
        _windowMinutes = configuration.GetValue<int>("DetectionDeduplication:WindowMinutes", 30);
    }

    public async Task<IReadOnlyList<DetectionEventDto>> DeduplicateAsync(
        IReadOnlyList<DetectionEventDto> events,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || events.Count == 0)
            return events;

        var deduped = new List<DetectionEventDto>(events.Count);
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-_windowMinutes);

        foreach (var ev in events)
        {
            var fp = DetectionFingerprint.Generate(ev);

            if (await CheckAndMarkAsync(fp, now, cutoff, cancellationToken))
            {
                _logger.LogTrace("Dedup: suppressed {Type} (fp={Fp})", ev.Type, fp[..12]);
                continue;
            }

            deduped.Add(ev);
        }

        return deduped;
    }

    public async Task<bool> IsDuplicateAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled) return false;
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-_windowMinutes);
        return await CheckAndMarkInternalAsync(fingerprintHash, now, cutoff, cancellationToken);
    }

    public async Task MarkSeenAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled) return;
        var now = DateTime.UtcNow;
        _cache[fingerprintHash] = new CachedFingerprint { LastSeen = now, HitCount = 1 };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.DetectionFingerprints
                .FirstOrDefaultAsync(f => f.Fingerprint == fingerprintHash, cancellationToken);

            if (existing == null)
            {
                db.DetectionFingerprints.Add(new DetectionFingerprintEntity
                {
                    Fingerprint = fingerprintHash,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    HitCount = 1,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "DB mark-seen failed for fp={Fp}", fingerprintHash[..12]);
        }
    }

    public async Task<int> CleanupStaleEntriesAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled) return 0;
        var cutoff = DateTime.UtcNow.AddHours(-24);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stale = await db.DetectionFingerprints
                .Where(f => f.LastSeenAt < cutoff)
                .ToListAsync(cancellationToken);
            db.DetectionFingerprints.RemoveRange(stale);
            await db.SaveChangesAsync(cancellationToken);
            if (stale.Count > 0)
                _logger.LogInformation("Cleaned {Count} stale detection fingerprints", stale.Count);
            return stale.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up stale fingerprints");
            return 0;
        }
    }

    private async Task<bool> CheckAndMarkAsync(
        string fp, DateTime now, DateTime cutoff, CancellationToken ct)
    {
        // In-memory hot cache
        if (_cache.TryGetValue(fp, out var cached) && cached.LastSeen >= cutoff)
            return true;

        return await CheckAndMarkInternalAsync(fp, now, cutoff, ct);
    }

    private async Task<bool> CheckAndMarkInternalAsync(
        string fp, DateTime now, DateTime cutoff, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.DetectionFingerprints
                .FirstOrDefaultAsync(f => f.Fingerprint == fp, ct);

            if (existing != null)
            {
                var originalLastSeen = existing.LastSeenAt;
                existing.HitCount++;
                existing.LastSeenAt = now;

                if (originalLastSeen >= cutoff)
                {
                    await db.SaveChangesAsync(ct);
                    _cache[fp] = new CachedFingerprint { LastSeen = now, HitCount = existing.HitCount };
                    return true;
                }

                existing.FirstSeenAt = now;
                await db.SaveChangesAsync(ct);
                _cache[fp] = new CachedFingerprint { LastSeen = now, HitCount = existing.HitCount };
                return false;
            }

            db.DetectionFingerprints.Add(new DetectionFingerprintEntity
            {
                Fingerprint = fp,
                FirstSeenAt = now,
                LastSeenAt = now,
                HitCount = 1,
            });
            await db.SaveChangesAsync(ct);
            _cache[fp] = new CachedFingerprint { LastSeen = now, HitCount = 1 };
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "DB dedup check failed for fp={Fp}", fp[..12]);
            return false;
        }
    }

    private sealed class CachedFingerprint
    {
        public DateTime LastSeen { get; set; }
        public int HitCount { get; set; }
    }
}
