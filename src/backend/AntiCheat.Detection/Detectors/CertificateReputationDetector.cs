using System.Collections.Concurrent;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Certificate Reputation", "1.0.0", "Online CRL/OCSP verification of file digital certificates")]
public class CertificateReputationDetector : IDetector
{
    private readonly ILogger<CertificateReputationDetector> _logger;
    private readonly ICertificateReputationService _certService;
    private readonly ConcurrentDictionary<string, DateTime> _reportedKeys = new();
    private static readonly TimeSpan DedupCooldown = TimeSpan.FromMinutes(5);

    public string Name => "Certificate Reputation";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public CertificateReputationDetector(
        ILogger<CertificateReputationDetector> logger,
        ICertificateReputationService certService)
    {
        _logger = logger;
        _certService = certService;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();
        var now = DateTime.UtcNow;

        // Clean stale entries
        foreach (var kvp in _reportedKeys)
        {
            if (now - kvp.Value > DedupCooldown * 2)
                _reportedKeys.TryRemove(kvp.Key, out _);
        }

        try
        {
            var evidence = await _certService.ScanGameFilesAsync(cancellationToken);

            foreach (var ev in evidence)
            {
                // Skip informational-only findings (unsigned files, expired-but-otherwise-valid certs)
                if (ev.Severity == "info")
                    continue;

                // Build dedup key from thumbprint + evidence type
                var dedupKey = $"{ev.Thumbprint}:{ev.EvidenceType}";

                if (_reportedKeys.TryGetValue(dedupKey, out var lastReported))
                {
                    if (now - lastReported < DedupCooldown)
                        continue;
                }

                _reportedKeys[dedupKey] = now;

                results.Add(new DetectionEventDto
                {
                    Type = $"Certificate: {ev.EvidenceType}",
                    Severity = ev.Severity,
                    Description = ev.Description,
                    Confidence = SeverityToConfidence(ev.Severity),
                    Timestamp = now,
                    ProcessName = ev.FileName,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate reputation scan failed");
        }

        _logger.LogDebug("CertificateReputationDetector: {Count} events after dedup", results.Count);
        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }

    private static double SeverityToConfidence(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 0.9,
        "high" => 0.75,
        "medium" => 0.5,
        _ => 0.0,
    };
}
