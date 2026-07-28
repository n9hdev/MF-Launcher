using System.Collections.Concurrent;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class CorrelationEngine : ICorrelationEngine
{
    private readonly ILogger<CorrelationEngine> _logger;
    private readonly ConcurrentDictionary<string, double> _scores = new();
    private static readonly string[] EmptySignals = Array.Empty<string>();

    public double CurrentCorrelationScore
    {
        get { return 0; } // Deprecated — use GetPlayerScore(playerId)
    }

    public double GetPlayerScore(string playerId)
    {
        return _scores.GetValueOrDefault(playerId, 0);
    }

    private static readonly Dictionary<string, double> SignalWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RWX"] = 25,
        ["Injection"] = 40,
        ["Module"] = 30,
        ["Timing"] = 20,
        ["Memory"] = 25,
        ["Thread"] = 35,
        ["Startup"] = 20,
        ["Hidden"] = 30,
    };

    private static readonly Dictionary<string, string> DetectorSignalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Memory Region Analyzer"] = "Memory",
        ["Anti-Injection Monitor"] = "Injection",
        ["Module Integrity Scanner"] = "Module",
        ["Injection Timing Analyzer"] = "Timing",
        ["Memory Scanner"] = "RWX",
        ["Injection Detector"] = "Injection",
        ["Process Analyzer"] = "Thread",
    };

    public CorrelationEngine(ILogger<CorrelationEngine> logger)
    {
        _logger = logger;
    }

    public CorrelationResultDto Evaluate(IReadOnlyList<DetectionEventDto> events, string? playerId = null)
    {
        var result = new CorrelationResultDto();

        if (events.Count == 0)
        {
            result.CorrelationScore = 0;
            result.Signals = new List<CorrelationSignalDto>();
            result.CorrelationLevel = "normal";
            result.AdjustedScanInterval = 30;
            if (!string.IsNullOrWhiteSpace(playerId)) _scores[playerId] = 0;
            return result;
        }

        var signalBuckets = new Dictionary<string, (double MaxConfidence, int Count)>(8);

        foreach (var ev in events)
        {
            var signalType = MapEventToSignal(ev);

            if (signalBuckets.TryGetValue(signalType, out var existing))
            {
                if (ev.Confidence > existing.MaxConfidence)
                    signalBuckets[signalType] = (ev.Confidence, existing.Count + 1);
                else
                    signalBuckets[signalType] = (existing.MaxConfidence, existing.Count + 1);
            }
            else
            {
                signalBuckets[signalType] = (ev.Confidence, 1);
            }
        }

        double totalScore = 0;
        var signals = new List<CorrelationSignalDto>(signalBuckets.Count);

        foreach (var kvp in signalBuckets)
        {
            var weight = SignalWeights.GetValueOrDefault(kvp.Key, 10);
            var (maxConfidence, count) = kvp.Value;

            var boost = count > 1 ? Math.Min(1.0 + (count - 1) * 0.15, 1.5) : 1.0;
            var contribution = weight * maxConfidence * boost;

            signals.Add(new CorrelationSignalDto
            {
                SignalType = kvp.Key,
                Weight = weight,
                Confidence = maxConfidence,
                Contribution = contribution,
            });

            totalScore += contribution;
        }

        totalScore = Math.Min(totalScore, 100);

        if (signals.Count >= 3)
        {
            totalScore = Math.Min(totalScore * 1.2, 100);
        }

        result.CorrelationScore = Math.Round(totalScore, 1);
        result.Signals = signals;
        result.CorrelationLevel = ClassifyCorrelation(totalScore);
        result.AutoEscalate = totalScore >= 95;

        result.AdjustedScanInterval = totalScore switch
        {
            >= 80 => 10,
            >= 60 => 15,
            >= 40 => 20,
            _ => 30,
        };

        if (!string.IsNullOrWhiteSpace(playerId)) _scores[playerId] = result.CorrelationScore;

        if (result.CorrelationScore >= 60)
        {
            _logger.LogWarning(
                "Correlation score {Score} ({Level}) -- {SignalCount} signal types -- auto-escalate: {Escalate}",
                result.CorrelationScore, result.CorrelationLevel,
                signals.Count, result.AutoEscalate);
        }

        return result;
    }

    public string ClassifyCorrelation(double score)
    {
        return score switch
        {
            >= 95 => "critical",
            >= 80 => "high",
            >= 60 => "suspicious",
            >= 30 => "elevated",
            _ => "normal"
        };
    }

    private static string MapEventToSignal(DetectionEventDto ev)
    {
        var type = ev.Type;

        if (type.Contains("RWX", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("PE Header", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Shellcode", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Allocation", StringComparison.OrdinalIgnoreCase))
            return "Memory";

        if (type.Contains("Thread", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Injected", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Injection", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("DLL", StringComparison.OrdinalIgnoreCase))
            return "Injection";

        if (type.Contains("Module", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Unsigned", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Hidden", StringComparison.OrdinalIgnoreCase))
            return "Module";

        if (type.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Timing", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Surge", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Spike", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Attachment", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Late", StringComparison.OrdinalIgnoreCase))
            return "Timing";

        return "Other";
    }
}
