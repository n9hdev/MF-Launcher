using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ReputationService : IReputationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReputationService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private readonly Dictionary<string, (ReputationLookupResponse Response, DateTime CachedAt)> _cache = new();
    private readonly object _cacheLock = new();

    public ReputationService(AppDbContext db, ILogger<ReputationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReputationLookupResponse> LookupAsync(ReputationLookupRequest request, CancellationToken ct = default)
    {
        var sha256 = request.Sha256?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sha256))
        {
            return new ReputationLookupResponse { Verdict = "unknown", ConfidenceScore = 0 };
        }

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(sha256, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
            {
                return cached.Response;
            }
        }

        var entry = await _db.FileReputation
            .FirstOrDefaultAsync(e => e.Sha256 == sha256, ct);

        if (entry == null)
        {
            var unknown = new ReputationLookupResponse
            {
                Sha256 = sha256,
                Verdict = "unknown",
                ConfidenceScore = 0,
            };
            CacheResponse(sha256, unknown);
            return unknown;
        }

        entry.LastSeen = DateTime.UtcNow;
        entry.TimesSeen++;
        await _db.SaveChangesAsync(ct);

        var response = MapToResponse(entry);
        CacheResponse(sha256, response);
        return response;
    }

    public async Task<ReputationLookupResponse> ReportAsync(ReputationReportRequest request, CancellationToken ct = default)
    {
        var sha256 = request.Sha256?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sha256))
        {
            return new ReputationLookupResponse { Verdict = "unknown", ConfidenceScore = 0 };
        }

        var entry = await _db.FileReputation.FirstOrDefaultAsync(e => e.Sha256 == sha256, ct);

        if (entry == null)
        {
            entry = new FileReputationEntity
            {
                Sha256 = sha256,
                Md5 = request.Md5,
                FileSize = request.FileSize,
                ProductName = request.ProductName,
                FileVersion = request.FileVersion,
                Signer = request.Signer,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TimesSeen = 1,
                UniquePlayers = 1,
                Verdict = "unknown",
            };
            _db.FileReputation.Add(entry);
        }
        else
        {
            entry.LastSeen = DateTime.UtcNow;
            entry.TimesSeen++;

            if (!string.IsNullOrEmpty(request.Signer) && string.IsNullOrEmpty(entry.Signer))
                entry.Signer = request.Signer;
            if (!string.IsNullOrEmpty(request.ProductName) && string.IsNullOrEmpty(entry.ProductName))
                entry.ProductName = request.ProductName;
            if (!string.IsNullOrEmpty(request.FileVersion) && string.IsNullOrEmpty(entry.FileVersion))
                entry.FileVersion = request.FileVersion;
            if (request.FileSize.HasValue && !entry.FileSize.HasValue)
                entry.FileSize = request.FileSize;
        }

        if (request.IsFlagged)
        {
            entry.TimesFlagged++;

            if (entry.Verdict == "unknown" || entry.Verdict == "safe")
            {
                entry.Verdict = "suspicious";
            }
        }

        entry.ConfidenceScore = ComputeConfidence(entry);
        await _db.SaveChangesAsync(ct);
        InvalidateCache(sha256);

        _logger.LogInformation("Reputation reported: {Sha256} verdict={Verdict} score={Score:F2} seen={Seen} flagged={Flagged}",
            sha256, entry.Verdict, entry.ConfidenceScore, entry.TimesSeen, entry.TimesFlagged);

        return MapToResponse(entry);
    }

    public async Task<ReputationLookupResponse> SetVerdictAsync(ReputationVerdictRequest request, CancellationToken ct = default)
    {
        var sha256 = request.Sha256?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sha256))
        {
            return new ReputationLookupResponse { Verdict = "unknown", ConfidenceScore = 0 };
        }

        var entry = await _db.FileReputation.FirstOrDefaultAsync(e => e.Sha256 == sha256, ct);

        if (entry == null)
        {
            entry = new FileReputationEntity
            {
                Sha256 = sha256,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                Verdict = request.Verdict,
                AnalysisNotes = request.Notes,
                IsLocalOverride = true,
                TimesSeen = 1,
            };
            _db.FileReputation.Add(entry);
        }
        else
        {
            entry.Verdict = request.Verdict;
            entry.AnalysisNotes = request.Notes;
            entry.IsLocalOverride = true;
            entry.LastAnalysisTime = DateTime.UtcNow;
        }

        entry.ConfidenceScore = request.Verdict == "cheat" ? -100 :
                                request.Verdict == "suspicious" ? -30 :
                                request.Verdict == "safe" ? 100 : 0;

        await _db.SaveChangesAsync(ct);
        InvalidateCache(sha256);

        _logger.LogInformation("Reputation verdict set: {Sha256} → {Verdict} (admin override)", sha256, request.Verdict);

        return MapToResponse(entry);
    }

    public async Task<int> GetTotalEntriesAsync(CancellationToken ct = default)
    {
        return await _db.FileReputation.CountAsync(ct);
    }

    private static ReputationLookupResponse MapToResponse(FileReputationEntity entry)
    {
        return new ReputationLookupResponse
        {
            Sha256 = entry.Sha256,
            Verdict = entry.Verdict,
            ConfidenceScore = entry.ConfidenceScore,
            IsOverride = entry.IsLocalOverride,
            AnalysisNotes = entry.AnalysisNotes,
        };
    }

    private static double ComputeConfidence(FileReputationEntity entry)
    {
        double score = 0;

        if (entry.TimesSeen > 1000 && entry.TimesFlagged == 0)
            score += 50;
        else if (entry.TimesSeen > 100 && entry.TimesFlagged == 0)
            score += 30;
        else if (entry.TimesSeen > 10)
            score += 10;

        if (entry.TimesFlagged == 0 && entry.TimesSeen > 10)
            score += 20;

        double flagRate = entry.TimesSeen > 0 ? (double)entry.TimesFlagged / entry.TimesSeen : 0;
        if (flagRate > 0.5)
            score -= 60;
        else if (flagRate > 0.1)
            score -= 30;
        else if (flagRate > 0.01)
            score -= 10;

        if (!string.IsNullOrEmpty(entry.Signer))
            score += 15;

        if (!string.IsNullOrEmpty(entry.ProductName) && !string.IsNullOrEmpty(entry.FileVersion))
            score += 10;

        if (entry.UniquePlayers > 100 && entry.TimesFlagged == 0)
            score += 20;

        if (entry.IsLocalOverride)
        {
            score = entry.Verdict == "cheat" ? -100 :
                    entry.Verdict == "safe" ? 100 :
                    entry.Verdict == "suspicious" ? -30 : 0;
        }

        return Math.Clamp(score, -100, 100);
    }

    private void CacheResponse(string sha256, ReputationLookupResponse response)
    {
        lock (_cacheLock)
        {
            _cache[sha256] = (response, DateTime.UtcNow);
        }
    }

    private void InvalidateCache(string sha256)
    {
        lock (_cacheLock)
        {
            _cache.Remove(sha256);
        }
    }
}
