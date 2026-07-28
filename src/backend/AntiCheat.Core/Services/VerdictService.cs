using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Services;

public class VerdictService : IVerdictService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VerdictService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    // Detector weights — more specific/accurate detectors get higher weight
    // Weights reduced for detectors with MTA-aware tuning to prevent false-positive inflation
    private static readonly Dictionary<string, double> DetectorWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Memory Scanner"] = 0.55,
        ["Memory Scanner Adv"] = 0.65,
        ["Behavioral Monitor"] = 0.75,
        ["YARA Rules"] = 0.65,
        ["Game Integrity"] = 0.75,
        ["PE Analyzer"] = 0.60,
        ["Process Analyzer"] = 0.55,
        ["Injection Detector"] = 0.55,
        ["Kernel Scanner"] = 0.65,
        ["ClamAV Scanner"] = 0.60,
        ["Team Cymru MHR"] = 0.55,
        ["Certificate Reputation"] = 0.50,
        ["Module Integrity"] = 0.50,
        ["Memory Region Analyzer"] = 0.50,
        ["Injection Timing Analyzer"] = 0.50,
        ["Anti-Tamper"] = 0.55,
        ["Anti-Injection Monitor"] = 0.50,
    };

    private const double DefaultDetectorWeight = 0.50;

    public VerdictService(
        IServiceScopeFactory scopeFactory,
        ILogger<VerdictService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<VerdictResult> EvaluateAsync(
        IReadOnlyList<DetectionEventDto> events,
        string? playerId = null,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<VerdictContribution>();
        var totalWeightedScore = 0.0;
        var totalWeight = 0.0;

        var grouped = events
            .GroupBy(e => e.Type.Split(':').First().Trim())
            .ToList();

        foreach (var group in grouped)
        {
            var detectorKey = group.Key;
            var weight = DetectorWeights.GetValueOrDefault(detectorKey, DefaultDetectorWeight);
            var topEvent = group.OrderByDescending(e => e.Confidence).First();

            // Group score: max confidence in group × weight
            var maxConfidence = group.Max(e => e.Confidence);
            var groupScore = maxConfidence * weight;

            totalWeightedScore += groupScore;
            totalWeight += weight;

            contributions.Add(new VerdictContribution
            {
                DetectorType = detectorKey,
                Weight = weight,
                ContributionScore = Math.Round(groupScore, 4),
                TopEvent = $"{topEvent.Type}: {topEvent.Description} (conf: {topEvent.Confidence})",
            });
        }

        // Normalize by total possible weight
        var finalConfidence = totalWeight > 0
            ? Math.Round(Math.Min(totalWeightedScore / totalWeight, 1.0), 4)
            : 0.0;

        // Count severity levels
        int critical = events.Count(e => e.Severity == "critical");
        int high = events.Count(e => e.Severity == "high");
        int medium = events.Count(e => e.Severity == "medium");

        // Consider detection frequency bonus
        double frequencyBonus = ComputeFrequencyBonus(playerId);
        finalConfidence = Math.Round(Math.Min(finalConfidence + frequencyBonus, 1.0), 4);

        // Apply MTA-environment penalty: if all events come from known MTA/gta_sa processes,
        // reduce confidence since MTA creates legitimate noise (CEF JIT, Lua, hooks, overlays)
        var mtaPenalty = ComputeMtaPenalty(events);
        finalConfidence = Math.Round(Math.Max(finalConfidence - mtaPenalty, 0.0), 4);

        var (verdict, action, explanation) = ClassifyVerdict(finalConfidence, critical, high, medium, events.Count);
        var escalation = events.Count >= 5 && (critical + high) >= 3;

        var result = new VerdictResult
        {
            FinalConfidence = finalConfidence,
            Verdict = verdict,
            SuggestedAction = action,
            Explanation = explanation,
            Contributions = contributions,
            TotalEvents = events.Count,
            CriticalCount = critical,
            HighCount = high,
            MediumCount = medium,
            AssessedAt = DateTime.UtcNow,
            EscalationRequired = escalation,
        };

        // Persist to DB (skip gracefully when AppDbContext is not registered, e.g. on Service side)
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<AppDbContext>();
            if (db != null)
            {
                var entity = new VerdictEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    PlayerId = playerId,
                    FinalConfidence = finalConfidence,
                    Verdict = verdict,
                    SuggestedAction = action,
                    Explanation = explanation,
                    TotalEvents = events.Count,
                    CriticalCount = critical,
                    HighCount = high,
                    MediumCount = medium,
                    ContributionsJson = JsonSerializer.Serialize(contributions, JsonOpts),
                    EscalationRequired = escalation,
                    AssessedAt = DateTime.UtcNow,
                };
                db.Verdicts.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist verdict");
        }

        return result;
    }

    public async Task<VerdictResult> GetLastVerdictAsync(string? playerId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Verdicts
                .Where(v => v.PlayerId == playerId || (playerId == null && v.PlayerId == null))
                .OrderByDescending(v => v.AssessedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                return new VerdictResult
                {
                    Verdict = "unknown",
                    Explanation = "No verdict has been recorded yet",
                };
            }

            var contributions = string.IsNullOrWhiteSpace(entity.ContributionsJson)
                ? new List<VerdictContribution>()
                : JsonSerializer.Deserialize<List<VerdictContribution>>(entity.ContributionsJson, JsonOpts) ?? new();

            return new VerdictResult
            {
                FinalConfidence = entity.FinalConfidence,
                Verdict = entity.Verdict,
                SuggestedAction = entity.SuggestedAction,
                Explanation = entity.Explanation,
                Contributions = contributions,
                TotalEvents = entity.TotalEvents,
                CriticalCount = entity.CriticalCount,
                HighCount = entity.HighCount,
                MediumCount = entity.MediumCount,
                AssessedAt = entity.AssessedAt,
                EscalationRequired = entity.EscalationRequired,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve last verdict");
            return new VerdictResult { Verdict = "unknown", Explanation = "Error retrieving verdict" };
        }
    }

    private static (string verdict, string action, string explanation) ClassifyVerdict(
        double confidence, int critical, int high, int medium, int total)
    {
        if (confidence >= 0.95 || critical >= 3 || (critical >= 2 && high >= 2))
            return ("cheat", "ban", $"High-confidence detection: {critical} critical, {high} high-severity events across {total} detections");

        if (confidence >= 0.75 || (high >= 3 && medium >= 2))
            return ("suspicious", "flag", $"Suspicious activity: {high} high, {medium} medium-severity events (confidence: {confidence})");

        if (confidence >= 0.45 || medium >= 4)
            return ("suspicious", "warn", $"Low-confidence signals detected: {medium} medium events (confidence: {confidence})");

        if (confidence > 0)
            return ("low_risk", "none", $"Minimal signals detected (confidence: {confidence})");

        return ("clean", "none", "No threats detected");
    }

    private static double ComputeMtaPenalty(IReadOnlyList<DetectionEventDto> events)
    {
        if (events.Count == 0) return 0;

        // If all events come from known MTA processes, apply a penalty
        var mtaProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gta_sa", "gta_sa.exe", "mtasa", "mtasa.exe",
            "multiplayer_sa", "multiplayer_sa.exe",
        };

        var allFromMta = events.All(e =>
            string.IsNullOrWhiteSpace(e.ProcessName) ||
            mtaProcesses.Contains(e.ProcessName.Trim()));

        if (!allFromMta) return 0;

        // Base penalty: reduce confidence by 0.10 since MTA environment generates legitimate noise
        var penalty = 0.10;

        // Reduce penalty for high-confidence events (critical severity)
        var criticalCount = events.Count(e => e.Severity == "critical");
        var highCount = events.Count(e => e.Severity == "high");

        // If there are critical or multiple high-severity events, reduce the penalty
        if (criticalCount >= 1) penalty = Math.Max(0.03, penalty - (criticalCount * 0.03));
        if (highCount >= 3) penalty = Math.Max(0.05, penalty - (highCount * 0.01));

        return penalty;
    }

    private double ComputeFrequencyBonus(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recentCount = db.Verdicts
                .Count(v => v.PlayerId == playerId
                    && v.AssessedAt > DateTime.UtcNow.AddHours(-1));

            if (recentCount >= 20) return 0.10;
            if (recentCount >= 10) return 0.05;
            if (recentCount >= 5) return 0.03;
        }
        catch
        {
            // DB unavailable — skip bonus
        }

        return 0;
    }
}
