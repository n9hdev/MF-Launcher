using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Behavioral Monitor", "1.0.0", "Watches process handles, unsigned modules, and suspicious threads targeting the game process")]
public class BehavioralDetector : IDetector
{
    private readonly ILogger<BehavioralDetector> _logger;
    private readonly IBehavioralMonitorService _monitor;

    public string Name => "Behavioral Monitor";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public BehavioralDetector(
        ILogger<BehavioralDetector> logger,
        IBehavioralMonitorService monitor)
    {
        _logger = logger;
        _monitor = monitor;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var evidence = await _monitor.ScanAsync(cancellationToken);

            foreach (var ev in evidence)
            {
                var (confidence, severity) = MapEvidence(ev);
                results.Add(new DetectionEventDto
                {
                    Type = $"{ev.Category}: {ev.Observation}",
                    Severity = severity,
                    Description = $"{ev.Interpretation}",
                    Confidence = confidence,
                    ProcessName = ev.ProcessName,
                    Timestamp = ev.Timestamp,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Behavioral monitor scan failed");
        }

        return results;
    }

    private static (double Confidence, string Severity) MapEvidence(EvidenceFact fact)
    {
        if (fact.Category == "process_handle")
        {
            bool hasAllAccess = fact.Details.TryGetValue("DesiredAccess", out var access) &&
                access == "0x1FFFFF";
            if (hasAllAccess)
                return (0.85, "high");
            return (0.50, "medium");
        }
        if (fact.Category == "unsigned_module")
            return (0.80, "high");
        if (fact.Category == "suspicious_thread")
            return (0.75, "high");
        if (fact.Category == "handle" && fact.IsAnomaly)
            return (0.70, "medium");
        if (fact.Tags.Contains("process_handle") || fact.Tags.Contains("unsigned_module"))
            return (0.70, "medium");
        return (0.0, "info");
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
