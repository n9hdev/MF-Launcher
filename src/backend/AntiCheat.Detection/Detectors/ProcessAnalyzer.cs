using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Process Analyzer", "1.0.0", "Scans running processes for known cheat software and suspicious names")]
public class ProcessAnalyzer : IDetector
{
    private readonly ILogger<ProcessAnalyzer> _logger;
    private readonly IWhitelistProvider _whitelist;
    private static readonly HashSet<string> KnownCheatProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cheatengine", "cheat_engine", "artmoney", "tsearch",
        "wpe_pro", "wireshark", "fiddler", "httppacketcatcher",
        "injector", "extreme_injector", "extreme injector", "perx", "processhacker",
    };

    private static readonly HashSet<string> KnownCheatPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "cheatengine", "cheat_engine", "artmoney",
        "injector", "extreme_injector", "extreme injector", "processhacker",
    };

    private static readonly HashSet<string> SafeProcessPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "anticheat", "mafia city anti-cheat", "mafiacityanticheat",
    };

    public string Name => "Process Analyzer";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWinTrustData);

    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
        new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    public ProcessAnalyzer(ILogger<ProcessAnalyzer> logger, IWhitelistProvider whitelist)
    {
        _logger = logger;
        _whitelist = whitelist;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var processName = process.ProcessName.ToLowerInvariant();

                    if (KnownCheatProcesses.Contains(processName) || KnownCheatPatterns.Any(p => processName.Contains(p)))
                    {
                        results.Add(new DetectionEventDto
                        {
                            Type = "Known Cheat Process",
                            Severity = "critical",
                            Description = $"Known cheat process detected: {process.ProcessName} (PID: {process.Id})",
                            Confidence = 0.95,
                            ProcessName = process.ProcessName,
                        });
                        continue;
                    }

                    if (await _whitelist.IsProcessWhitelistedAsync(processName, cancellationToken))
                        continue;

                    if ((processName.Contains("inject") || processName.Contains("hack") || processName.Contains("cheat"))
                        && !SafeProcessPrefixes.Any(p => processName.StartsWith(p)))
                    {
                        results.Add(new DetectionEventDto
                        {
                            Type = "Suspicious Process Name",
                            Severity = "critical",
                            Description = $"Suspicious process: {process.ProcessName} (PID: {process.Id})",
                            Confidence = 0.85,
                            ProcessName = process.ProcessName,
                        });
                    }
                }
                catch (Exception exInner)
                {
                    _logger.LogTrace(exInner, "Skipped inaccessible process: {ProcessName}", process.ProcessName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process analysis failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
