using System.Diagnostics;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Advanced Memory Scanner", "1.0.0", "Evidence-only memory analysis: baseline snapshot + delta monitoring — no verdicts, no confidence")]
public class AdvancedMemoryDetector : IDetector
{
    private readonly ILogger<AdvancedMemoryDetector> _logger;
    private readonly IBaselineService _baselineService;
    private readonly IDeltaMonitorService _deltaMonitor;

    public string Name => "Advanced Memory Scanner";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public AdvancedMemoryDetector(
        ILogger<AdvancedMemoryDetector> logger,
        IBaselineService baselineService,
        IDeltaMonitorService deltaMonitor)
    {
        _logger = logger;
        _baselineService = baselineService;
        _deltaMonitor = deltaMonitor;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var gameProcess = Process.GetProcessesByName("gta_sa")
                .FirstOrDefault() ?? Process.GetProcessesByName("MTA")
                .FirstOrDefault();

            if (gameProcess == null)
                return results;

            var pid = gameProcess.Id;
            var processName = gameProcess.ProcessName;

            // Phase 2: Capture baseline (only once)
            var baseline = await _baselineService.CaptureBaselineAsync(pid, processName, cancellationToken);

            // Convert baseline observations to evidence
            foreach (var entry in baseline.Modules.Entries)
            {
                var apisPresent = new List<string>();
                if (entry.PresentInPeb) apisPresent.Add("PEB");
                if (entry.PresentInToolhelp) apisPresent.Add("Toolhelp");
                if (entry.PresentInPsapi) apisPresent.Add("PSAPI");

                if (apisPresent.Count < 3)
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = "Baseline: Module Enumeration Discrepancy",
                        Severity = "info",
                        Description = $"Module {entry.ModuleName} at 0x{entry.BaseAddress:X} visible in {string.Join("+", apisPresent)} but not in all APIs",
                        Confidence = 0.0,
                        ProcessName = processName,
                    });
                }
            }

            // Phase 3: Delta monitoring
            var deltaFacts = await _deltaMonitor.ScanDeltaAsync(baseline, cancellationToken);
            foreach (var fact in deltaFacts)
            {
                results.Add(new DetectionEventDto
                {
                    Type = $"{fact.Category}: {fact.Observation}",
                    Severity = "info",
                    Description = fact.Interpretation,
                    Confidence = 0.0,
                    ProcessName = fact.ProcessName,
                    Timestamp = fact.Timestamp,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advanced memory scanner scan failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
