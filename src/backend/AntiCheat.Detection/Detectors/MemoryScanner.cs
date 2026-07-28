using System.Diagnostics;
using System.Runtime.InteropServices;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Memory Scanner", "1.1.0", "Scans process memory for RWX executable pages via VirtualQueryEx; filters MEM_IMAGE and known MTA module ranges")]
public class MemoryScanner : IDetector
{
    private readonly ILogger<MemoryScanner> _logger;
    private readonly IWhitelistProvider _whitelist;
    private readonly IMtaBaselineProvider _mtaBaseline;
    public string Name => "Memory Scanner";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

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
    private const uint MEM_PRIVATE = 0x20000;
    private const uint MEM_IMAGE = 0x1000000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
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

    public MemoryScanner(ILogger<MemoryScanner> logger, IWhitelistProvider whitelist, IMtaBaselineProvider mtaBaseline)
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
                            var modName = sb.ToString();
                            moduleRanges.Add((modInfo.lpBaseOfDll.ToInt64(), modInfo.lpBaseOfDll.ToInt64() + modInfo.SizeOfImage, modName));
                        }
                    }
                }

                var address = IntPtr.Zero;
                var pageCount = 0;
                var suspiciousRwxCount = 0;

                while (VirtualQueryEx(hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != false)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (++pageCount > MaxMemoryPages)
                        break;

                    if (mbi.State != MEM_COMMIT || mbi.Protect != PAGE_EXECUTE_READWRITE)
                    {
                        var regionSize = mbi.RegionSize.ToInt64();
                        if (regionSize <= 0 || regionSize > int.MaxValue)
                            regionSize = 0x1000;
                        address = IntPtr.Add(mbi.BaseAddress, (int)regionSize);
                        continue;
                    }

                    // Skip MEM_IMAGE pages (legitimate DLL code/data sections)
                    if (mbi.Type == MEM_IMAGE)
                    {
                        var regionSize = mbi.RegionSize.ToInt64();
                        if (regionSize <= 0 || regionSize > int.MaxValue)
                            regionSize = 0x1000;
                        address = IntPtr.Add(mbi.BaseAddress, (int)regionSize);
                        continue;
                    }

                    // Check if address falls within a known module range
                    var addr = mbi.BaseAddress.ToInt64();
                    bool inKnownModule = false;
                    string? owningModule = null;
                    foreach (var mod in moduleRanges)
                    {
                        if (addr >= mod.Start && addr < mod.End)
                        {
                            inKnownModule = true;
                            owningModule = mod.Name;
                            break;
                        }
                    }

                    if (inKnownModule)
                    {
                        var regionSize = mbi.RegionSize.ToInt64();
                        if (regionSize <= 0 || regionSize > int.MaxValue)
                            regionSize = 0x1000;
                        address = IntPtr.Add(mbi.BaseAddress, (int)regionSize);
                        continue;
                    }

                    // Only private RWX pages are suspicious
                    if (mbi.Type != MEM_PRIVATE)
                    {
                        var regionSize = mbi.RegionSize.ToInt64();
                        if (regionSize <= 0 || regionSize > int.MaxValue)
                            regionSize = 0x1000;
                        address = IntPtr.Add(mbi.BaseAddress, (int)regionSize);
                        continue;
                    }

                    suspiciousRwxCount++;
                    results.Add(new DetectionEventDto
                    {
                        Type = "RWX Memory Region",
                        Severity = suspiciousRwxCount > 3 ? "high" : "medium",
                        Description = $"Private RWX memory page at 0x{mbi.BaseAddress.ToInt64():X} size {mbi.RegionSize.ToInt64():N0}",
                        Confidence = suspiciousRwxCount > 3 ? 0.75 : 0.65,
                        ProcessName = targetProcess.ProcessName,
                    });

                    var sz = mbi.RegionSize.ToInt64();
                    if (sz <= 0 || sz > int.MaxValue)
                        sz = 0x1000;

                    address = IntPtr.Add(mbi.BaseAddress, (int)sz);
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
            _logger.LogError(ex, "Memory scan failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
