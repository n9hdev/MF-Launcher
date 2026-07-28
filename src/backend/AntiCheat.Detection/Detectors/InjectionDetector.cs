using System.Diagnostics;
using System.Runtime.InteropServices;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Injection Detector", "1.1.0", "Enumerates process modules and detects known malicious or suspicious DLLs; filters known overlay DLLs and MTA modules")]
public class InjectionDetector : IDetector
{
    private readonly ILogger<InjectionDetector> _logger;
    private readonly IMtaBaselineProvider _mtaBaseline;
    public string Name => "Injection Detector";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule,
        uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule,
        System.Text.StringBuilder lpBaseName, uint nSize);

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    private static readonly HashSet<string> KnownInjectedDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "eac_dll.dll", "be_dll.dll", "vgtray.dll",
        "cheatengine-x86_64.dll", "inject.dll", "extreme.dll",
    };

    public InjectionDetector(ILogger<InjectionDetector> logger, IMtaBaselineProvider mtaBaseline)
    {
        _logger = logger;
        _mtaBaseline = mtaBaseline;
    }

    public Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var targetProcess = Process.GetProcessesByName("gta_sa")
                .FirstOrDefault() ?? Process.GetProcessesByName("MTA")
                .FirstOrDefault();

            if (targetProcess == null)
                return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, targetProcess.Id);

            if (hProcess == IntPtr.Zero)
                return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);

            try
            {
                var modules = new IntPtr[1024];
                if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
                {
                    var moduleCount = (int)(needed / (uint)IntPtr.Size);
                    for (var i = 0; i < moduleCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var sb = new System.Text.StringBuilder(256);
                        GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                        var dllName = sb.ToString();

                        // Skip known MTA modules and known overlay/legitimate modules
                        if (_mtaBaseline.IsKnownMtaModule(dllName) || _mtaBaseline.IsKnownOverlayOrLegitimateModule(dllName))
                            continue;

                        if (KnownInjectedDlls.Contains(dllName))
                        {
                            results.Add(new DetectionEventDto
                            {
                                Type = "Known Injected DLL",
                                Severity = "critical",
                                Description = $"Known malicious DLL loaded: {dllName}",
                                Confidence = 0.98,
                                ProcessName = targetProcess.ProcessName,
                            });
                        }
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Injection detection failed");
        }

        return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
