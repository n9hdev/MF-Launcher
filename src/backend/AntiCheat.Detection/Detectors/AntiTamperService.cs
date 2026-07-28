using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Reflection;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Anti-Tamper Service", "1.0.0", "Self-protection: debugger detection via PEB/NtQueryInformationProcess, DLL integrity hashing, and detector module whitelist")]
public class AntiTamperService : IDetector
{
    private readonly ILogger<AntiTamperService> _logger;
    private readonly Dictionary<string, string> _baselineHashes = new(StringComparer.OrdinalIgnoreCase);
    private AntiTamperStatusDto _lastStatus = new();

    public string Name => "Anti-Tamper Service";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, IntPtr processInformation, int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int ProcessDebugPort = 7;
    private const int ProcessDebugObjectHandle = 30;
    private const int ProcessDebugFlags = 31;

    private static readonly HashSet<string> WhitelistedDetectorAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AntiCheat.Detection.dll",
        "AntiCheat.Core.dll",
        "AntiCheat.Shared.dll",
        "AntiCheat.Api.dll",
    };

    public AntiTamperService(ILogger<AntiTamperService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();
        var status = new AntiTamperStatusDto();

        try
        {
            var debuggerDetected = CheckDebuggerPresent();
            status.DebuggerDetected = debuggerDetected;

            if (debuggerDetected)
            {
                results.Add(new DetectionEventDto
                {
                    Type = "Debugger Detected",
                    Severity = "critical",
                    Description = "Debugger presence detected on anti-cheat process via NtQueryInformationProcess and PEB flags",
                    Confidence = 0.95,
                    ProcessName = Process.GetCurrentProcess().ProcessName,
                });

                status.Alerts.Add("Debugger detected on anti-cheat host process");
            }

            var currentAsm = Assembly.GetExecutingAssembly();
            var location = Path.GetDirectoryName(currentAsm.Location);

            if (location != null && Directory.Exists(location))
            {
                foreach (var dll in Directory.GetFiles(location, "*.dll"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(dll);

                    if (!WhitelistedDetectorAssemblies.Contains(fileName) &&
                        !fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var hash = ComputeSha256(dll);

                    if (_baselineHashes.TryGetValue(fileName, out var previousHash))
                    {
                        if (!string.Equals(hash, previousHash, StringComparison.OrdinalIgnoreCase))
                        {
                            status.FailedModules++;
                            status.Alerts.Add($"Integrity failure: {fileName} hash mismatch");

                            results.Add(new DetectionEventDto
                            {
                                Type = "Detector DLL Tampered",
                                Severity = "critical",
                                Description = $"Anti-cheat DLL '{fileName}' hash has changed — possible tampering or hooking",
                                Confidence = 0.98,
                                ProcessName = Process.GetCurrentProcess().ProcessName,
                            });
                        }
                        else
                        {
                            status.VerifiedModules++;
                        }
                    }
                    else
                    {
                        _baselineHashes[fileName] = hash;
                        status.VerifiedModules++;
                    }
                }
            }

            status.IntegrityCheckPassed = status.FailedModules == 0 && !debuggerDetected;
            status.LastChecked = DateTime.UtcNow;
            _lastStatus = status;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anti-tamper check failed");
        }

        return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);
    }

    public AntiTamperStatusDto GetCurrentStatus()
    {
        return _lastStatus;
    }

    private static bool CheckDebuggerPresent()
    {
        try
        {
            var hProcess = Process.GetCurrentProcess().Handle;
            var buffer = Marshal.AllocHGlobal(4);
            try
            {
                var status = NtQueryInformationProcess(hProcess, ProcessDebugPort, buffer, 4, out _);
                if (status == 0 && Marshal.ReadInt32(buffer) != 0)
                    return true;

                Marshal.WriteInt32(buffer, 0);
                status = NtQueryInformationProcess(hProcess, ProcessDebugObjectHandle, buffer, 4, out _);
                if (status == 0 && Marshal.ReadInt32(buffer) != 0)
                    return true;

                Marshal.WriteInt32(buffer, 0);
                status = NtQueryInformationProcess(hProcess, ProcessDebugFlags, buffer, 4, out _);
                if (status == 0 && Marshal.ReadInt32(buffer) == 0)
                    return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }

        try
        {
            var kernel32 = GetModuleHandle("kernel32.dll");
            if (kernel32 != IntPtr.Zero)
            {
                var isDebugPresent = GetProcAddress(kernel32, "IsDebuggerPresent");
                if (isDebugPresent != IntPtr.Zero)
                {
                    var debuggerPresent = Marshal.GetDelegateForFunctionPointer<IsDebuggerPresentDelegate>(isDebugPresent);
                    if (debuggerPresent())
                        return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private delegate bool IsDebuggerPresentDelegate();

    private static string ComputeSha256(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
