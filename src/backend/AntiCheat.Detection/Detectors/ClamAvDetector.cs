using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("ClamAV Scanner", "1.0.0", "Server-side ClamAV daemon scan of game executables")]
public class ClamAvDetector : IDetector
{
    private readonly ILogger<ClamAvDetector> _logger;
    private readonly IClamAvService _clamAv;

    public string Name => "ClamAV Scanner";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public ClamAvDetector(
        ILogger<ClamAvDetector> logger,
        IClamAvService clamAv)
    {
        _logger = logger;
        _clamAv = clamAv;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        if (!_clamAv.IsAvailable)
        {
            _logger.LogDebug("ClamAV not available — skipping scan");
            return results;
        }

        try
        {
            var scanResults = await _clamAv.ScanGameDirectoryAsync(cancellationToken);

            foreach (var r in scanResults)
            {
                if (r.ScanResult == "clean" || r.ScanResult == "skipped")
                    continue;

                results.Add(new DetectionEventDto
                {
                    Type = r.EvidenceType,
                    Severity = r.Severity,
                    Description = r.Description,
                    Confidence = r.IsInfected ? 0.85 : 0.0,
                    Timestamp = DateTime.UtcNow,
                    ProcessName = r.FileName,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
