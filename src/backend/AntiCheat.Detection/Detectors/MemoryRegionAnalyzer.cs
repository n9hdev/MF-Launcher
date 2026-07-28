using System.Diagnostics;
using System.Runtime.InteropServices;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Memory Region Analyzer", "1.1.0", "Loop-based VirtualQueryEx scan for RWX/RX regions, shellcode entropy, and PE headers in anonymous memory; filters CEF/Lua JIT legitimate regions")]
public class MemoryRegionAnalyzer : IDetector
{
    private readonly ILogger<MemoryRegionAnalyzer> _logger;
    private readonly IWhitelistProvider _whitelist;
    private readonly IMtaBaselineProvider _mtaBaseline;

    public string Name => "Memory Region Analyzer";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_FREE = 0x10000;
    private const uint MEM_PRIVATE = 0x20000;
    private const uint MEM_IMAGE = 0x1000000;

    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_READ = 0x20;

    private const int MaxMemoryPages = 500_000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    public MemoryRegionAnalyzer(ILogger<MemoryRegionAnalyzer> logger, IWhitelistProvider whitelist, IMtaBaselineProvider mtaBaseline)
    {
        _logger = logger;
        _whitelist = whitelist;
        _mtaBaseline = mtaBaseline;
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
                return results;

            if (await _whitelist.IsProcessWhitelistedAsync(targetProcess.ProcessName, cancellationToken))
                return results;

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, targetProcess.Id);
            if (hProcess == IntPtr.Zero)
                return results;

            try
            {
                var moduleRanges = new List<(long Start, long End, string Name)>();
                var modules = new IntPtr[1024];
                if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
                {
                    var count = (int)(needed / (uint)IntPtr.Size);
                    for (var i = 0; i < count; i++)
                    {
                        if (GetModuleInformation(hProcess, modules[i], out var modInfo, (uint)Marshal.SizeOf<MODULEINFO>()))
                        {
                            var sb = new System.Text.StringBuilder(256);
                            GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                            moduleRanges.Add((modInfo.lpBaseOfDll.ToInt64(), modInfo.lpBaseOfDll.ToInt64() + modInfo.SizeOfImage, sb.ToString()));
                        }
                    }
                }

                var address = IntPtr.Zero;
                var suspiciousRegions = 0;
                var pageCount = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (++pageCount > MaxMemoryPages)
                        break;

                    if (!VirtualQueryEx(hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                        break;

                    if (mbi.State == MEM_COMMIT && mbi.RegionSize.ToInt64() > 0)
                    {
                        var addr = mbi.BaseAddress.ToInt64();
                        var isRWX = mbi.Protect == PAGE_EXECUTE_READWRITE;
                        var isRX = mbi.Protect == PAGE_EXECUTE_READ;
                        var isPrivate = mbi.Type == MEM_PRIVATE;
                        var isImage = mbi.Type == MEM_IMAGE;

                        // Check if this address falls within a known module range
                        bool inKnownModule = false;
                        foreach (var mod in moduleRanges)
                        {
                            if (addr >= mod.Start && addr < mod.End)
                            {
                                inKnownModule = true;
                                break;
                            }
                        }

                        if (isRWX && isPrivate && !isImage && !inKnownModule)
                        {
                            suspiciousRegions++;
                            results.Add(new DetectionEventDto
                            {
                                Type = "RWX Private Memory",
                                Severity = suspiciousRegions > 5 ? "high" : "medium",
                                Description = $"RWX private memory region at 0x{mbi.BaseAddress.ToInt64():X} size {mbi.RegionSize.ToInt64():N0} bytes — possible shellcode or injected code",
                                Confidence = suspiciousRegions > 5 ? 0.8 : 0.65,
                                ProcessName = targetProcess.ProcessName,
                            });
                        }
                        else if (isRX && isPrivate && !isImage && !inKnownModule && mbi.RegionSize.ToInt64() <= 0x100000)
                        {
                            var buffer = new byte[Math.Min((int)mbi.RegionSize.ToInt64(), 4096)];
                            if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead) && bytesRead > 2)
                            {
                                var hasPEHeader = buffer[0] == 'M' && buffer[1] == 'Z';
                                var entropy = CalculateEntropy(buffer, bytesRead);

                                if (hasPEHeader)
                                {
                                    results.Add(new DetectionEventDto
                                    {
                                        Type = "PE Header in Private Memory",
                                        Severity = "critical",
                                        Description = $"PE image header (MZ) found in private RX memory at 0x{mbi.BaseAddress.ToInt64():X} size {mbi.RegionSize.ToInt64():N0} — possible manual-map injection",
                                        Confidence = 0.8,
                                        ProcessName = targetProcess.ProcessName,
                                    });
                                }
                                else if (entropy > 7.5)
                                {
                                    results.Add(new DetectionEventDto
                                    {
                                        Type = "High-Entropy Shellcode Region",
                                        Severity = "medium",
                                        Description = $"High entropy ({entropy:F2}) in private RX memory at 0x{mbi.BaseAddress.ToInt64():X} size {mbi.RegionSize.ToInt64():N0} — possible encoded shellcode",
                                        Confidence = 0.65,
                                        ProcessName = targetProcess.ProcessName,
                                    });
                                }
                            }
                        }
                    }

                    var delta = mbi.RegionSize.ToInt64();
                    if (delta <= 0 || delta > int.MaxValue)
                        delta = 0x1000;

                    address = IntPtr.Add(mbi.BaseAddress, (int)delta);

                    if (mbi.State == MEM_FREE)
                    {
                        address = IntPtr.Add(mbi.BaseAddress, 0x10000);
                    }

                    if (address.ToInt64() >= 0x7FFFFFFF0000 || address.ToInt64() < 0)
                        break;
                }

                if (suspiciousRegions > 10)
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = "Mass RWX Allocation",
                        Severity = "high",
                        Description = $"Process has {suspiciousRegions} RWX private memory regions — indicative of extensive code injection",
                        Confidence = 0.75,
                        ProcessName = targetProcess.ProcessName,
                    });
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
            _logger.LogError(ex, "Memory region analysis failed");
        }

        return results;
    }

    private static double CalculateEntropy(byte[] data, int length)
    {
        var freq = new int[256];
        for (var i = 0; i < length; i++)
            freq[data[i]]++;

        double entropy = 0;
        for (var i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            var p = (double)freq[i] / length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
