using System.Diagnostics;
using System.Runtime.InteropServices;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Anti-Injection Monitor", "1.1.0", "Tracks thread creation via Toolhelp32 snapshot; detects remote threads, external origins, and suspicious start addresses")]
public class AntiInjectionMonitor : IDetector
{
    private readonly ILogger<AntiInjectionMonitor> _logger;
    private readonly IMtaBaselineProvider _mtaBaseline;
    private readonly Dictionary<int, List<ThreadEntry>> _baseline = new();
    private int _scanCount;

    public string Name => "Anti-Injection Monitor";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryInformationThread(IntPtr threadHandle, int threadInformationClass, IntPtr threadInformation, int threadInformationLength, out int returnLength);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    private const uint TH32CS_SNAPTHREAD = 0x00000004;
    private const uint THREAD_QUERY_INFORMATION = 0x0040;
    private const uint THREAD_GET_CONTEXT = 0x0008;
    private const int ThreadQuerySetWin32StartAddress = 9;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct THREADENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ThreadID;
        public uint th32OwnerProcessID;
        public long tpBasePri;
        public long tpDeltaPri;
        public uint dwFlags;
    }

    private struct ThreadEntry
    {
        public uint ThreadId;
    }

    public AntiInjectionMonitor(ILogger<AntiInjectionMonitor> logger, IMtaBaselineProvider mtaBaseline)
    {
        _logger = logger;
        _mtaBaseline = mtaBaseline;
    }

    public Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();
        _scanCount++;

        try
        {
            var targetProcess = Process.GetProcessesByName("gta_sa")
                .FirstOrDefault() ?? Process.GetProcessesByName("MTA")
                .FirstOrDefault();

            if (targetProcess == null)
                return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);

            var currentThreads = new List<ThreadEntry>();
            var hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);

            if (hSnapshot == IntPtr.Zero)
                return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);

            try
            {
                var te = new THREADENTRY32
                {
                    dwSize = (uint)Marshal.SizeOf<THREADENTRY32>()
                };

                if (Thread32First(hSnapshot, ref te))
                {
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (te.th32OwnerProcessID == (uint)targetProcess.Id)
                        {
                            currentThreads.Add(new ThreadEntry { ThreadId = te.th32ThreadID });
                        }
                    }
                    while (Thread32Next(hSnapshot, ref te));
                }

                var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, targetProcess.Id);
                var moduleAddresses = new List<(IntPtr Base, IntPtr End, string Name)>();

                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var modules = new IntPtr[1024];
                        if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
                        {
                            var moduleCount = (int)(needed / (uint)IntPtr.Size);
                            for (var i = 0; i < moduleCount; i++)
                            {
                                if (GetModuleInformation(hProcess, modules[i], out var modInfo, (uint)Marshal.SizeOf<MODULEINFO>()))
                                {
                                    var sb = new System.Text.StringBuilder(256);
                                    GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                                    var modName = sb.ToString();
                                    moduleAddresses.Add((modInfo.lpBaseOfDll, IntPtr.Add(modInfo.lpBaseOfDll, (int)modInfo.SizeOfImage), modName));
                                }
                            }
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }

                if (moduleAddresses.Count == 0)
                {
                    try
                    {
                        var mainModule = targetProcess.MainModule;
                        if (mainModule != null)
                        {
                            var baseAddr = mainModule.BaseAddress;
                            moduleAddresses.Add((baseAddr, IntPtr.Add(baseAddr, mainModule.ModuleMemorySize), mainModule.ModuleName));
                        }
                    }
                    catch { }
                }

                foreach (var thread in currentThreads)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hThread = OpenThread(THREAD_QUERY_INFORMATION | THREAD_GET_CONTEXT, false, thread.ThreadId);
                    if (hThread == IntPtr.Zero)
                        continue;

                    try
                    {
                        var startAddrPtr = Marshal.AllocHGlobal(IntPtr.Size);
                        try
                        {
                            var status = NtQueryInformationThread(hThread, ThreadQuerySetWin32StartAddress, startAddrPtr, IntPtr.Size, out _);
                            if (status == 0)
                            {
                                var startAddress = Marshal.ReadIntPtr(startAddrPtr).ToInt64();

                                var inKnownModule = false;
                                string? owningModule = null;
                                foreach (var mod in moduleAddresses)
                                {
                                    if (startAddress >= mod.Base.ToInt64() && startAddress < mod.End.ToInt64())
                                    {
                                        inKnownModule = true;
                                        owningModule = mod.Name;
                                        break;
                                    }
                                }

                                if (!inKnownModule && startAddress > 0x10000)
                                {
                                    // Skip if start address is in known MTA or overlay module
                                    results.Add(new DetectionEventDto
                                    {
                                        Type = "Suspicious Thread Start Address",
                                        Severity = "high",
                                        Description = $"Thread {thread.ThreadId} in {targetProcess.ProcessName} has start address 0x{startAddress:X} outside any loaded module — possible reflective/manual-map injection",
                                        Confidence = 0.85,
                                        ProcessName = targetProcess.ProcessName,
                                    });
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(startAddrPtr);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        CloseHandle(hThread);
                    }
                }

                if (_baseline.TryGetValue(targetProcess.Id, out var previous))
                {
                    // Raised threshold from +3 to +10 to accommodate MTA's dynamic thread model
                    // (CEF alone creates 15-20 threads during gameplay)
                    if (currentThreads.Count > previous.Count + 10)
                    {
                        var newThreads = currentThreads
                            .Where(t => !previous.Any(p => p.ThreadId == t.ThreadId))
                            .ToList();

                        if (newThreads.Count > 5)
                        {
                            results.Add(new DetectionEventDto
                            {
                                Type = "Thread Count Spike",
                                Severity = "low",
                                Description = $"Thread count for {targetProcess.ProcessName} increased from {previous.Count} to {currentThreads.Count} ({newThreads.Count} new threads) — possible thread injection",
                                Confidence = 0.5,
                                ProcessName = targetProcess.ProcessName,
                            });
                        }
                    }
                }

                _baseline[targetProcess.Id] = currentThreads;
            }
            finally
            {
                CloseHandle(hSnapshot);
            }

            if (_scanCount > 100)
            {
                _baseline.Clear();
                _scanCount = 0;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anti-injection scan failed");
        }

        return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
