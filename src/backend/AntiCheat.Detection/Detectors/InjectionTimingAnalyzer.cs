using System.Diagnostics;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Injection Timing Analyzer", "1.1.0", "Delta-based injection analysis: monitors module/thread/memory changes — evidence only")]
public class InjectionTimingAnalyzer : IDetector
{
    private readonly ILogger<InjectionTimingAnalyzer> _logger;
    private readonly IBaselineService _baselineService;
    private readonly IDeltaMonitorService _deltaMonitor;
    private BaselineSnapshot? _cachedBaseline;

    public string Name => "Injection Timing Analyzer";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    public InjectionTimingAnalyzer(
        ILogger<InjectionTimingAnalyzer> logger,
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
            var targetProcess = Process.GetProcessesByName("gta_sa")
                .FirstOrDefault() ?? Process.GetProcessesByName("MTA")
                .FirstOrDefault();

            if (targetProcess == null)
            {
                _cachedBaseline = null;
                return results;
            }

            var pid = targetProcess.Id;
            var processName = targetProcess.ProcessName;

            if (_cachedBaseline == null)
            {
                _cachedBaseline = await _baselineService.CaptureBaselineAsync(pid, processName, cancellationToken);

                results.Add(new DetectionEventDto
                {
                    Type = "Timing: Baseline Captured",
                    Severity = "info",
                    Description = $"Baseline captured for {processName} (PID {pid}): {_cachedBaseline.Modules.Entries.Count} modules, {_cachedBaseline.Threads.Count} threads, {_cachedBaseline.Regions.Count} memory regions",
                    Confidence = 0.0,
                    ProcessName = processName,
                });
            }
            else
            {
                var deltaFacts = await _deltaMonitor.ScanDeltaAsync(_cachedBaseline, cancellationToken);

                foreach (var fact in deltaFacts)
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = $"Timing: {fact.Observation}",
                        Severity = "info",
                        Description = fact.Interpretation,
                        Confidence = 0.0,
                        ProcessName = fact.ProcessName,
                        Timestamp = fact.Timestamp,
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Injection timing analysis failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
