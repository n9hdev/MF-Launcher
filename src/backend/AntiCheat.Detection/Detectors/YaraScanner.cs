using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("YARA Scanner", "2.0.0", "Multi-signature engine: process names, PE metadata, file paths against MTA cheat rules")]
public partial class YaraDetector : IDetector
{
    private readonly ILogger<YaraDetector> _logger;
    private readonly ISignatureEngine _engine;
    private readonly IPeAnalysisService _peAnalysis;
    private readonly IWhitelistProvider _whitelist;
    private readonly int _maxProcessesPerScan;

    public string Name => "YARA Scanner";
    public string Version => "2.0.0";
    public bool IsEnabled { get; set; } = true;

    public YaraDetector(
        ILogger<YaraDetector> logger,
        ISignatureEngine engine,
        IPeAnalysisService peAnalysis,
        IWhitelistProvider whitelist,
        int maxProcessesPerScan = 50)
    {
        _logger = logger;
        _engine = engine;
        _peAnalysis = peAnalysis;
        _whitelist = whitelist;
        _maxProcessesPerScan = maxProcessesPerScan;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();

            foreach (var process in processes.Take(_maxProcessesPerScan))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var processName = process.ProcessName;

                    if (await _whitelist.IsProcessWhitelistedAsync(processName))
                        continue;

                    // Threat-intelligent: check process name against known cheat patterns
                    // (cheat-processes.json) — this catches actual cheat tools by name.
                    var processMatches = _engine.MatchProcessName(processName);
                    foreach (var match in processMatches)
                    {
                        results.Add(new DetectionEventDto
                        {
                            Type = $"YARA: {match.RuleName}",
                            Severity = match.Severity,
                            Description = $"{match.Description}",
                            Confidence = SeverityToConfidence(match.Severity),
                            ProcessName = processName,
                        });
                    }

                    try
                    {
                        var modulePath = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(modulePath))
                        {
                            // Check file path against known cheat path patterns
                            var pathMatches = _engine.MatchFilePath(modulePath);
                            foreach (var match in pathMatches)
                            {
                                results.Add(new DetectionEventDto
                                {
                                    Type = $"YARA: {match.RuleName}",
                                    Severity = match.Severity,
                                    Description = $"{match.Description}",
                                    Confidence = SeverityToConfidence(match.Severity),
                                    ProcessName = processName,
                                });
                            }

                            // PE analysis (MatchPe) on external processes is intentionally SKIPPED.
                            // Rules like LowEntropyCodeSection, ExecutableWritableSection_RWX, and
                            // TlsCallbacks_Present fire on virtually every modern binary (Chrome, VS Code,
                            // dotnet, Node, etc.) — these are structural observations, not threat indicators.
                            // PE analysis is performed in ScanCheatDirectoriesAsync and AnalyzeExternalFileAsync
                            // for files in cheat directories and the game process only.
                        }
                    }
                    catch (Exception modEx)
                    {
                        _logger.LogTrace(modEx, "Skipped inaccessible process: {Process}", processName);
                    }
                }
                catch (Exception procEx)
                {
                    _logger.LogTrace(procEx, "Skipped inaccessible process: {Process}", process.ProcessName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YARA scan failed");
        }

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
        "medium" => 0.6,
        "low" => 0.3,
        _ => 0.5,
    };
}
