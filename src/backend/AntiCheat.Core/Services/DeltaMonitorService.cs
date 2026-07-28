using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class DeltaMonitorService : IDeltaMonitorService
{
    private readonly ILogger<DeltaMonitorService> _logger;
    private readonly IWhitelistProvider _whitelist;

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPTHREAD = 0x00000004;
    private const uint THREAD_QUERY_INFORMATION = 0x0040;
    private const int ThreadQuerySetWin32StartAddress = 0x09;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationThread(IntPtr hThread, int ThreadInformationClass, IntPtr ThreadInformation, int ThreadInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct THREADENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ThreadID;
        public uint th32OwnerProcessID;
        public int tpBasePri;
        public int tpDeltaPri;
        public uint dwFlags;
    }

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

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_FREE = 0x10000;
    private const uint MEM_PRIVATE = 0x20000;
    private const uint MEM_IMAGE = 0x1000000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const ulong MAX_ADDRESS = 0x7FFFFFFF;

    public DeltaMonitorService(
        ILogger<DeltaMonitorService> logger,
        IWhitelistProvider whitelist)
    {
        _logger = logger;
        _whitelist = whitelist;
    }

    public async Task<IReadOnlyList<EvidenceFact>> ScanDeltaAsync(BaselineSnapshot baseline, CancellationToken ct = default)
    {
        var facts = new List<EvidenceFact>();

        try
        {
            var gameProcess = Process.GetProcessesByName("gta_sa").FirstOrDefault()
                          ?? Process.GetProcessesByName("MTA").FirstOrDefault();

            if (gameProcess == null || gameProcess.Id != baseline.ProcessId)
            {
                facts.Add(new EvidenceFact
                {
                    Category = "process",
                    Observation = $"Target process {baseline.ProcessName} (PID: {baseline.ProcessId}) no longer running",
                    Interpretation = "Process exited since baseline capture",
                    SourceComponent = "DeltaMonitor",
                    ProcessName = baseline.ProcessName,
                    ProcessId = baseline.ProcessId,
                });
                return facts;
            }

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, gameProcess.Id);
            if (hProcess == IntPtr.Zero)
                return facts;

            try
            {
                var moduleFacts = await CheckModuleDeltaAsync(hProcess, gameProcess.Id, baseline, ct);
                facts.AddRange(moduleFacts);

                var threadFacts = await CheckThreadDeltaAsync(gameProcess.Id, baseline, ct);
                facts.AddRange(threadFacts);

                var memoryFacts = await CheckMemoryDeltaAsync(hProcess, baseline, ct);
                facts.AddRange(memoryFacts);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delta monitor scan failed");
        }

        return facts;
    }

    private async Task<List<EvidenceFact>> CheckModuleDeltaAsync(IntPtr hProcess, int pid, BaselineSnapshot baseline, CancellationToken ct)
    {
        var facts = new List<EvidenceFact>();

        var currentModules = new Dictionary<ulong, (string Name, string Path)>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)pid);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return facts;

        try
        {
            var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (Module32First(snapshot, ref me))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();
                    var baseAddr = (ulong)me.modBaseAddr.ToInt64();
                    currentModules[baseAddr] = (me.szModule, me.szExePath);
                } while (Module32Next(snapshot, ref me));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        // New modules not in baseline
        foreach (var kv in currentModules)
        {
            var baselineEntry = baseline.Modules.Entries.FirstOrDefault(e => e.BaseAddress == kv.Key);
            if (baselineEntry == null)
            {
                var tags = new List<string> { "module", "new_module" };
                if (!await _whitelist.IsProcessWhitelistedAsync(kv.Value.Name))
                    tags.Add("untrusted_source");

                facts.Add(new EvidenceFact
                {
                    Category = "module",
                    Observation = $"Module loaded: {kv.Value.Name} at 0x{kv.Key:X}",
                    Interpretation = $"Path: {kv.Value.Path} — Module appeared after baseline capture",
                    SourceComponent = "DeltaMonitor.ModuleDelta",
                    ProcessName = baseline.ProcessName,
                    ProcessId = pid,
                    IsAnomaly = false,
                    Tags = tags,
                    Details = { ["ModuleName"] = kv.Value.Name, ["ModulePath"] = kv.Value.Path, ["BaseAddress"] = $"0x{kv.Key:X}" },
                });
            }
        }

        // Modules in baseline but no longer loaded
        foreach (var entry in baseline.Modules.Entries)
        {
            if (!currentModules.ContainsKey(entry.BaseAddress))
            {
                facts.Add(new EvidenceFact
                {
                    Category = "module",
                    Observation = $"Module unloaded: {entry.ModuleName} at 0x{entry.BaseAddress:X}",
                    Interpretation = "Module present at baseline has been unloaded — legitimate for some MTA modules",
                    SourceComponent = "DeltaMonitor.ModuleDelta",
                    ProcessName = baseline.ProcessName,
                    ProcessId = pid,
                    IsAnomaly = false,
                    Tags = { "module", "unloaded_module" },
                    Details = { ["ModuleName"] = entry.ModuleName },
                });
            }
        }

        return facts;
    }

    private async Task<List<EvidenceFact>> CheckThreadDeltaAsync(int pid, BaselineSnapshot baseline, CancellationToken ct)
    {
        var facts = new List<EvidenceFact>();

        var baselineThreadIds = new HashSet<uint>(baseline.Threads.Select(t => t.ThreadId));
        var currentThreadIds = new List<uint>();

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return facts;

        try
        {
            var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
            if (Thread32First(snapshot, ref te))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();
                    if ((int)te.th32OwnerProcessID == pid)
                        currentThreadIds.Add(te.th32ThreadID);
                } while (Thread32Next(snapshot, ref te));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        // New threads
        foreach (var tid in currentThreadIds)
        {
            if (!baselineThreadIds.Contains(tid))
            {
                ct.ThrowIfCancellationRequested();

                var startAddr = GetThreadStartAddress((int)tid);

                if (startAddr == 0)
                {
                    facts.Add(new EvidenceFact
                    {
                        Category = "thread",
                        Observation = $"New thread 0x{tid:X} created (start address unavailable)",
                        Interpretation = "New thread in process since baseline capture",
                        SourceComponent = "DeltaMonitor.ThreadDelta",
                        ProcessName = baseline.ProcessName,
                        ProcessId = pid,
                        IsAnomaly = false,
                        Tags = { "thread", "new_thread" },
                        Details = { ["ThreadId"] = $"0x{tid:X}" },
                    });
                }
                else
                {
                    var moduleName = baseline.Modules.ResolveModule(startAddr);
                    var offset = moduleName != null ? startAddr - baseline.Modules.Entries.FirstOrDefault(e => e.ModuleName == moduleName)?.BaseAddress ?? 0 : 0;
                    var resolved = moduleName != null ? $"{moduleName}+0x{offset:X}" : $"0x{startAddr:X}";

                    facts.Add(new EvidenceFact
                    {
                        Category = "thread",
                        Observation = $"New thread 0x{tid:X} created at {resolved}",
                        Interpretation = moduleName != null
                            ? $"Thread resolves to loaded module {moduleName}+0x{offset:X}"
                            : "Thread start address does not resolve to any known loaded module",
                        SourceComponent = "DeltaMonitor.ThreadDelta",
                        ProcessName = baseline.ProcessName,
                        ProcessId = pid,
                        IsAnomaly = moduleName == null,
                        Tags = { "thread", "new_thread" },
                        Details =
                        {
                            ["ThreadId"] = $"0x{tid:X}",
                            ["StartAddress"] = $"0x{startAddr:X}",
                            ["ResolvedModule"] = moduleName ?? "none",
                            ["Offset"] = $"0x{offset:X}",
                        },
                    });
                }
            }
        }

        // Threads that exited
        var currentThreadSet = new HashSet<uint>(currentThreadIds);
        foreach (var tid in baselineThreadIds)
        {
            if (!currentThreadSet.Contains(tid))
            {
                var baselineThread = baseline.Threads.FirstOrDefault(t => t.ThreadId == tid);
                facts.Add(new EvidenceFact
                {
                    Category = "thread",
                    Observation = $"Thread 0x{tid:X} exited (started at {baselineThread?.StartModuleName ?? "unknown"}+0x{baselineThread?.StartModuleOffset ?? 0:X})",
                    Interpretation = "Thread present at baseline has exited",
                    SourceComponent = "DeltaMonitor.ThreadDelta",
                    ProcessName = baseline.ProcessName,
                    ProcessId = pid,
                    IsAnomaly = false,
                    Tags = { "thread", "exited_thread" },
                    Details = { ["ThreadId"] = $"0x{tid:X}" },
                });
            }
        }

        return facts;
    }

    private async Task<List<EvidenceFact>> CheckMemoryDeltaAsync(IntPtr hProcess, BaselineSnapshot baseline, CancellationToken ct)
    {
        var facts = new List<EvidenceFact>();
        var baselineRegionMap = new Dictionary<ulong, MemoryRegionRecord>();

        foreach (var r in baseline.Regions)
        {
            baselineRegionMap[r.BaseAddress] = r;
        }

        var address = IntPtr.Zero;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!VirtualQueryEx(hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                break;

            if (mbi.State == MEM_COMMIT && mbi.RegionSize.ToInt64() > 0)
            {
                var baseAddr = (ulong)mbi.BaseAddress.ToInt64();
                var isExecutable = (mbi.Protect & 0xF0) != 0;
                var isRWX = mbi.Protect == PAGE_EXECUTE_READWRITE;
                var isPrivate = mbi.Type == MEM_PRIVATE;
                var size = mbi.RegionSize.ToInt64();

                // Check for new executable private regions
                if (isExecutable && isPrivate && !baselineRegionMap.ContainsKey(baseAddr))
                {
                    var containsPeHeader = false;
                    var buffer = new byte[Math.Min((int)size, 4096)];
                    if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead) && bytesRead >= 2)
                    {
                        containsPeHeader = buffer[0] == 'M' && buffer[1] == 'Z';
                    }

                    var tags = new List<string> { "memory", "new_region" };
                    if (isRWX) tags.Add("rwx");
                    if (containsPeHeader) tags.Add("pe_header");
                    if (size >= 65536) tags.Add("large_allocation");

                    facts.Add(new EvidenceFact
                    {
                        Category = "memory",
                        Observation = $"New {(isRWX ? "RWX" : "executable")} private region at 0x{baseAddr:X}, size {size:N0} bytes",
                        Interpretation = containsPeHeader
                            ? $"Contains PE header (MZ) — possible manual-map injection indicator"
                            : size >= 65536
                                ? $"Large allocation ({size:N0} bytes) in private memory"
                                : $"Small allocation ({size:N0} bytes) — may be JIT or normal operation",
                        SourceComponent = "DeltaMonitor.MemoryDelta",
                        ProcessName = baseline.ProcessName,
                        ProcessId = baseline.ProcessId,
                        IsAnomaly = containsPeHeader || (isRWX && size >= 65536),
                        Tags = tags,
                        Details =
                        {
                            ["Address"] = $"0x{baseAddr:X}",
                            ["Size"] = size.ToString("N0"),
                            ["Protection"] = $"0x{mbi.Protect:X8}",
                            ["ContainsPEHeader"] = containsPeHeader.ToString(),
                        },
                    });
                }
            }

            var delta = mbi.RegionSize.ToInt64();
            if (delta <= 0) delta = 0x1000;
            address = IntPtr.Add(mbi.BaseAddress, (int)delta);

            if (mbi.State == MEM_FREE)
                address = IntPtr.Add(mbi.BaseAddress, 0x10000);

            if ((ulong)address.ToInt64() >= MAX_ADDRESS || address.ToInt64() < 0)
                break;
        }

        return facts;
    }

    public async Task<IReadOnlyList<EvidenceFact>> CheckCodeIntegrityAsync(BaselineSnapshot baseline, CancellationToken ct = default)
    {
        var facts = new List<EvidenceFact>();

        try
        {
            var gameProcess = Process.GetProcessesByName("gta_sa").FirstOrDefault()
                          ?? Process.GetProcessesByName("MTA").FirstOrDefault();

            if (gameProcess == null || gameProcess.Id != baseline.ProcessId)
                return facts;

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, gameProcess.Id);
            if (hProcess == IntPtr.Zero)
                return facts;

            try
            {
                foreach (var module in baseline.Modules.Entries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (module.CodeHash == 0)
                        continue;

                    var buffer = new byte[Math.Min(module.SizeOfImage, 65536)];
                    if (!ReadProcessMemory(hProcess, new IntPtr((long)module.BaseAddress), buffer, buffer.Length, out var bytesRead) || bytesRead <= 0)
                        continue;

                    var currentHash = ComputeXXHash64(buffer, bytesRead);
                    if (currentHash != module.CodeHash)
                    {
                        facts.Add(new EvidenceFact
                        {
                            Category = "code_integrity",
                            Observation = $"Code section hash mismatch in {module.ModuleName} at 0x{module.BaseAddress:X}",
                            Interpretation = $"Baseline hash 0x{module.CodeHash:X16} != current hash 0x{currentHash:X16} — possible inline hook or code modification",
                            SourceComponent = "DeltaMonitor.CodeIntegrity",
                            ProcessName = baseline.ProcessName,
                            ProcessId = baseline.ProcessId,
                            IsAnomaly = true,
                            Tags = { "hash_mismatch", "code_integrity", module.ModuleName.Contains("mta") ? "mta_module" : "game_module" },
                            Details =
                            {
                                ["ModuleName"] = module.ModuleName,
                                ["BaseAddress"] = $"0x{module.BaseAddress:X}",
                                ["BaselineHash"] = $"0x{module.CodeHash:X16}",
                                ["CurrentHash"] = $"0x{currentHash:X16}",
                            },
                        });
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code integrity check failed");
        }

        return facts;
    }

    private static ulong ComputeXXHash64(byte[] data, int length)
    {
        const ulong p1 = 11400714785074694791ul;
        const ulong p2 = 14029467366897019727ul;
        const ulong p3 = 1609587929392839161ul;
        const ulong p4 = 9650029242287828579ul;
        const ulong p5 = 2870177450012600261ul;

        ulong h64;
        int index = 0;
        int remaining = length;

        if (remaining >= 32)
        {
            ulong s1 = unchecked(p2 + p1);
            ulong s2 = p2;
            ulong s3 = 0;
            ulong s4 = unchecked(p1 - p2);

            while (remaining >= 32)
            {
                s1 = Round(s1, BitConverter.ToUInt64(data, index)); index += 8;
                s2 = Round(s2, BitConverter.ToUInt64(data, index)); index += 8;
                s3 = Round(s3, BitConverter.ToUInt64(data, index)); index += 8;
                s4 = Round(s4, BitConverter.ToUInt64(data, index)); index += 8;
                remaining -= 32;
            }

            h64 = RotateLeft(s1, 1) + RotateLeft(s2, 7) + RotateLeft(s3, 12) + RotateLeft(s4, 18);
            h64 = MergeRound(h64, s1);
            h64 = MergeRound(h64, s2);
            h64 = MergeRound(h64, s3);
            h64 = MergeRound(h64, s4);
        }
        else
        {
            h64 = p5 + p1;
        }

        h64 += (ulong)length;

        while (remaining >= 8)
        {
            var k1 = BitConverter.ToUInt64(data, index);
            k1 *= p2; k1 = RotateLeft(k1, 31); k1 *= p1;
            h64 ^= k1; h64 = RotateLeft(h64, 27) * p1 + p4;
            index += 8; remaining -= 8;
        }

        while (remaining >= 4)
        {
            var k1 = (ulong)BitConverter.ToUInt32(data, index);
            k1 *= p1; k1 = RotateLeft(k1, 23); k1 *= p2;
            h64 ^= k1; h64 = RotateLeft(h64, 11) * p1 + p3;
            index += 4; remaining -= 4;
        }

        while (remaining > 0)
        {
            var k1 = (ulong)data[index];
            k1 *= p5; k1 = RotateLeft(k1, 11); k1 *= p1;
            h64 ^= k1; h64 = RotateLeft(h64, 19) * p1 + p4;
            index++; remaining--;
        }

        h64 ^= h64 >> 33;
        h64 *= p2;
        h64 ^= h64 >> 29;
        h64 *= p3;
        h64 ^= h64 >> 32;

        return h64;

        static ulong Round(ulong acc, ulong input)
        {
            input *= p2; input = RotateLeft(input, 31); input *= p1;
            acc ^= input; acc = RotateLeft(acc, 27) * p1 + p4;
            return acc;
        }

        static ulong MergeRound(ulong acc, ulong val)
        {
            val = Round(0, val);
            acc ^= val; acc = acc * p1 + p4;
            return acc;
        }

        static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }

    private static ulong GetThreadStartAddress(int threadId)
    {
        var hThread = OpenThread(THREAD_QUERY_INFORMATION, false, threadId);
        if (hThread == IntPtr.Zero) return 0;

        try
        {
            var buf = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                if (NtQueryInformationThread(hThread, ThreadQuerySetWin32StartAddress, buf, IntPtr.Size, out _) == 0)
                {
                    return (ulong)Marshal.ReadIntPtr(buf).ToInt64();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            CloseHandle(hThread);
        }

        return 0;
    }
}
