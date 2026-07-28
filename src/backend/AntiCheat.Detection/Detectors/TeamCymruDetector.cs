using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Team Cymru MHR", "1.0.0", "WHOIS-based hash reputation against 30+ AV vendors")]
public class TeamCymruDetector : IDetector
{
    private readonly ILogger<TeamCymruDetector> _logger;
    private readonly ITeamCymruService _cymru;

    public string Name => "Team Cymru MHR";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public TeamCymruDetector(
        ILogger<TeamCymruDetector> logger,
        ITeamCymruService cymru)
    {
        _logger = logger;
        _cymru = cymru;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        if (!_cymru.IsAvailable)
        {
            _logger.LogDebug("Team Cymru MHR not available — skipping");
            return results;
        }

        try
        {
            var scanResults = await _cymru.ScanGameDirectoryAsync(cancellationToken);

            foreach (var r in scanResults)
            {
                if (r.ScanResult == "clean" || r.ScanResult == "unknown" || r.ScanResult == "disabled")
                    continue;

                results.Add(new DetectionEventDto
                {
                    Type = r.EvidenceType,
                    Severity = r.Severity,
                    Description = r.Description,
                    Confidence = DetectionRateToConfidence(r.DetectionRate),
                    Timestamp = DateTime.UtcNow,
                    ProcessName = r.FileName,
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Team Cymru MHR scan failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }

    private static double DetectionRateToConfidence(double rate) => rate switch
    {
        >= 0.5 => 0.95,
        >= 0.2 => 0.8,
        >= 0.05 => 0.6,
        _ => 0.0,
    };
}
