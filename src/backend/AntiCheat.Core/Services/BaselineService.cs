using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class BaselineService : IBaselineService
{
    private readonly ILogger<BaselineService> _logger;

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_FREE = 0x10000;
    private const uint MEM_IMAGE = 0x1000000;
    private const uint MEM_MAPPED = 0x40000;
    private const uint MEM_PRIVATE = 0x20000;
    private const uint PAGE_EXECUTE = 0x10;
    private const uint PAGE_EXECUTE_READ = 0x20;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int ProcessInformationClass, out PROCESS_BASIC_INFORMATION pbi, int ProcessInformationLength, out int ReturnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationThread(IntPtr hThread, int ThreadInformationClass, IntPtr ThreadInformation, int ThreadInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);

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
    private struct PROCESS_BASIC_INFORMATION
    {
        public int ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LDR_DATA_TABLE_ENTRY_PARTIAL
    {
        public IntPtr InLoadOrderLinks_Flink;
        public IntPtr InLoadOrderLinks_Blink;
        public IntPtr InMemoryOrderLinks_Flink;
        public IntPtr InMemoryOrderLinks_Blink;
        public IntPtr InInitializationOrderLinks_Flink;
        public IntPtr InInitializationOrderLinks_Blink;
        public IntPtr DllBase;
        public IntPtr EntryPoint;
        public uint SizeOfImage;
        // FullDllName (UNICODE_STRING) starts at offset 0x30
        // BaseDllName (UNICODE_STRING) starts at offset 0x48
    }

    private const uint PE_SIGNATURE_OFFSET = 0x3C;
    private const uint PE_SIGNATURE_SIZE = 4;
    private const uint IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x020B;

    public BaselineService(ILogger<BaselineService> logger)
    {
        _logger = logger;
    }

    public async Task<BaselineSnapshot> CaptureBaselineAsync(int processId, string processName, CancellationToken ct = default)
    {
        var snapshot = new BaselineSnapshot
        {
            CaptureTime = DateTime.UtcNow,
            ProcessId = processId,
            ProcessName = processName,
        };

        var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            _logger.LogWarning("Cannot open process {Pid} for baseline capture", processId);
            return snapshot;
        }

        try
        {
            snapshot.ProcessStartTime = GetProcessStartTime(hProcess);

            var modulesTask = Task.Run(() => EnumerateModules(hProcess, processId, ct), ct);
            var regionsTask = Task.Run(() => WalkMemoryRegions(hProcess, ct), ct);
            var threadsTask = Task.Run(() => EnumerateThreads(processId, ct), ct);

            await Task.WhenAll(modulesTask, regionsTask, threadsTask);

            snapshot.Modules = modulesTask.Result;
            snapshot.Regions = regionsTask.Result;
            snapshot.Threads = threadsTask.Result;

            HashImageCodeSections(hProcess, snapshot, ct);

            snapshot.IsMtaInitialized = snapshot.Modules.Entries.Any(e =>
                e.ModuleName.Equals("mtasa.dll", StringComparison.OrdinalIgnoreCase));

            var mtaEntry = snapshot.Modules.Entries.FirstOrDefault(e =>
                e.ModuleName.Equals("mtasa.dll", StringComparison.OrdinalIgnoreCase));
            if (mtaEntry != null)
            {
                snapshot.MtaInstallPath = Path.GetDirectoryName(mtaEntry.FullPath) ?? string.Empty;
            }

            _logger.LogInformation(
                "Baseline captured: {ModuleCount} modules, {RegionCount} regions, {ThreadCount} threads, MTA={MtaPresent}",
                snapshot.Modules.Entries.Count, snapshot.Regions.Count, snapshot.Threads.Count, snapshot.IsMtaInitialized);
        }
        finally
        {
            CloseHandle(hProcess);
        }

        return snapshot;
    }

    public async Task<bool> WaitForMtaInitializationAsync(int processId, TimeSpan pollInterval, int stableCount, TimeSpan timeout, CancellationToken ct = default)
    {
        if (pollInterval == default) pollInterval = TimeSpan.FromSeconds(2);
        if (stableCount <= 0) stableCount = 3;
        if (timeout == default) timeout = TimeSpan.FromSeconds(15);

        var deadline = DateTime.UtcNow + timeout;
        var stablePolls = 0;
        var previousCount = 0;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, ct);

                var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)processId);
                if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                    continue;

                try
                {
                    var currentCount = 0;
                    var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
                    if (Module32First(snapshot, ref me))
                    {
                        do { currentCount++; } while (Module32Next(snapshot, ref me));
                    }

                    if (currentCount == previousCount && currentCount > 0)
                        stablePolls++;
                    else
                        stablePolls = 0;

                    if (stablePolls >= stableCount)
                    {
                        _logger.LogInformation("MTA module load stable at {Count} modules after {Polls} stable polls", currentCount, stablePolls);
                        return true;
                    }

                    previousCount = currentCount;
                    _logger.LogTrace("Module count: {Count}, stable polls: {Stable}", currentCount, stablePolls);
                }
                finally
                {
                    CloseHandle(snapshot);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Module count poll failed");
            }
        }

        _logger.LogWarning("MTA initialization wait timed out after {Timeout}s", timeout.TotalSeconds);
        return false;
    }

    private ModuleRangeMap EnumerateModules(IntPtr hProcess, int processId, CancellationToken ct)
    {
        var map = new ModuleRangeMap();
        var pebModules = new Dictionary<ulong, (uint Size, string Name, string Path)>();

        // 1) PEB Loader List (authoritative)
        try
        {
            if (NtQueryInformationProcess(hProcess, 0, out var pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _) == 0
                && pbi.PebBaseAddress != IntPtr.Zero)
            {
                var ldrFieldOffset = IntPtr.Size == 8 ? 0x18 : 0x0C;
                var ldrPtrBytes = new byte[IntPtr.Size];
                if (ReadProcessMemory(hProcess, pbi.PebBaseAddress + ldrFieldOffset, ldrPtrBytes, ldrPtrBytes.Length, out _))
                {
                    var ldrAddr = IntPtr.Size == 8
                        ? new IntPtr(BitConverter.ToInt64(ldrPtrBytes, 0))
                        : new IntPtr(BitConverter.ToInt32(ldrPtrBytes, 0));

                    if (ldrAddr != IntPtr.Zero)
                    {
                        var listHeadBytes = new byte[IntPtr.Size * 2];
                        if (ReadProcessMemory(hProcess, ldrAddr + 0x0C, listHeadBytes, listHeadBytes.Length, out _))
                        {
                            var flink = IntPtr.Size == 8
                                ? new IntPtr(BitConverter.ToInt64(listHeadBytes, 0))
                                : new IntPtr(BitConverter.ToInt32(listHeadBytes, 0));

                            var listHead = flink;
                            var visited = 0;

                            while (flink != IntPtr.Zero && visited < 300)
                            {
                                ct.ThrowIfCancellationRequested();
                                visited++;

                                var entryBytes = new byte[Marshal.SizeOf<LDR_DATA_TABLE_ENTRY_PARTIAL>()];
                                if (!ReadProcessMemory(hProcess, flink, entryBytes, entryBytes.Length, out _))
                                    break;

                                var dllBase = IntPtr.Size == 8
                                    ? new IntPtr(BitConverter.ToInt64(entryBytes, 0x18))
                                    : new IntPtr(BitConverter.ToInt32(entryBytes, 0x18));
                                var sizeOfImage = (uint)(IntPtr.Size == 8
                                    ? BitConverter.ToUInt32(entryBytes, 0x28)
                                    : BitConverter.ToUInt32(entryBytes, 0x24));

                                var baseAddr = (ulong)dllBase.ToInt64();

                                var nameLength = Marshal.ReadInt16(flink + 0x30);
                                var namePtr = Marshal.ReadIntPtr(flink + 0x38);

                                var fullNameLength = Marshal.ReadInt16(flink + 0x48);
                                var fullNamePtr = Marshal.ReadIntPtr(flink + 0x50);

                                var moduleName = ReadUnicodeString(hProcess, namePtr, nameLength);
                                var fullPath = ReadUnicodeString(hProcess, fullNamePtr, fullNameLength);

                                if (!string.IsNullOrWhiteSpace(moduleName) && !pebModules.ContainsKey(baseAddr))
                                {
                                    pebModules[baseAddr] = (sizeOfImage, moduleName, fullPath ?? string.Empty);
                                }

                                var nextLinkBytes = new byte[IntPtr.Size];
                                if (!ReadProcessMemory(hProcess, flink, nextLinkBytes, nextLinkBytes.Length, out _))
                                    break;

                                flink = IntPtr.Size == 8
                                    ? new IntPtr(BitConverter.ToInt64(nextLinkBytes, 0))
                                    : new IntPtr(BitConverter.ToInt32(nextLinkBytes, 0));

                                if (flink == listHead || flink == IntPtr.Zero)
                                    break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "PEB module enumeration failed");
        }

        // 2) Toolhelp32Snapshot (cross-reference)
        var toolhelpModules = new Dictionary<ulong, (string Name, string Path, uint Size)>();
        var toolhelpSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)processId);
        if (toolhelpSnapshot != IntPtr.Zero && toolhelpSnapshot != new IntPtr(-1))
        {
            try
            {
                var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
                if (Module32First(toolhelpSnapshot, ref me))
                {
                    do
                    {
                        ct.ThrowIfCancellationRequested();
                        var baseAddr = (ulong)me.modBaseAddr.ToInt64();
                        if (!toolhelpModules.ContainsKey(baseAddr))
                        {
                            toolhelpModules[baseAddr] = (me.szModule, me.szExePath, me.modBaseSize);
                        }
                    } while (Module32Next(toolhelpSnapshot, ref me));
                }
            }
            finally
            {
                CloseHandle(toolhelpSnapshot);
            }
        }

        // 3) EnumProcessModules (cross-reference)
        var psapiModules = new HashSet<ulong>();
        var psapiNames = new Dictionary<ulong, string>();
        var modules = new IntPtr[1024];
        if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
        {
            var count = (int)(needed / (uint)IntPtr.Size);
            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sb = new StringBuilder(256);
                GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                var name = sb.ToString();

                var nameSb = new StringBuilder(260);
                GetModuleFileNameEx(hProcess, modules[i], nameSb, (uint)nameSb.Capacity);
                var path = nameSb.ToString();

                psapiModules.Add((ulong)modules[i].ToInt64());
                psapiNames[(ulong)modules[i].ToInt64()] = name;
            }
        }

        // Merge all sources
        var allBases = new HashSet<ulong>();
        foreach (var kv in pebModules) allBases.Add(kv.Key);
        foreach (var kv in toolhelpModules) allBases.Add(kv.Key);

        var index = 0;
        foreach (var baseAddr in allBases.OrderBy(b => b))
        {
            ct.ThrowIfCancellationRequested();

            var pebInfo = pebModules.GetValueOrDefault(baseAddr);
            var thInfo = toolhelpModules.GetValueOrDefault(baseAddr);

            var entry = new ModuleEntry
            {
                BaseAddress = baseAddr,
                SizeOfImage = pebInfo.Size > 0 ? pebInfo.Size : thInfo.Size,
                ModuleName = pebInfo.Name ?? thInfo.Name ?? psapiNames.GetValueOrDefault(baseAddr) ?? $"module_{index}",
                FullPath = pebInfo.Path ?? thInfo.Path ?? string.Empty,
                PresentInPeb = pebModules.ContainsKey(baseAddr),
                PresentInToolhelp = toolhelpModules.ContainsKey(baseAddr),
                PresentInPsapi = psapiModules.Contains(baseAddr),
                LoadOrderIndex = index++,
            };

            map.Entries.Add(entry);
        }

        // Detect discrepancies: module in PEB but not Toolhelp (or vice versa)
        foreach (var kv in pebModules)
        {
            if (!toolhelpModules.ContainsKey(kv.Key))
            {
                _logger.LogTrace("PEB-only module (not in Toolhelp): {Name} at 0x{Addr:X}", kv.Value.Name, kv.Key);
            }
        }
        foreach (var kv in toolhelpModules)
        {
            if (!pebModules.ContainsKey(kv.Key))
            {
                _logger.LogTrace("Toolhelp-only module (not in PEB): {Name} at 0x{Addr:X}", kv.Value.Name, kv.Key);
            }
        }

        return map;
    }

    private List<MemoryRegionRecord> WalkMemoryRegions(IntPtr hProcess, CancellationToken ct)
    {
        var regions = new List<MemoryRegionRecord>();
        var address = IntPtr.Zero;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!VirtualQueryEx(hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                break;

            if (mbi.State == MEM_COMMIT && mbi.RegionSize.ToInt64() > 0)
            {
                var baseAddr = (ulong)mbi.BaseAddress.ToInt64();
                var size = mbi.RegionSize.ToInt64();
                var regionType = mbi.Type;
                var protect = mbi.Protect;

                bool isExecutable = (protect & 0xF0) != 0;
                bool isImage = regionType == MEM_IMAGE;

                var record = new MemoryRegionRecord
                {
                    BaseAddress = baseAddr,
                    RegionSize = size,
                    Protect = protect,
                    Type = regionType,
                };

                if (isImage && isExecutable)
                {
                    var hash = ComputeRegionHash(hProcess, mbi.BaseAddress, Math.Min(size, 65536));
                    record.CodeHash = hash;
                }

                if (isExecutable && regionType == MEM_PRIVATE)
                {
                    var buffer = new byte[Math.Min((int)size, 4096)];
                    if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead) && bytesRead >= 2)
                    {
                        record.ContainsPeHeader = buffer[0] == 'M' && buffer[1] == 'Z';
                    }
                }

                regions.Add(record);
            }

            var delta = mbi.RegionSize.ToInt64();
            if (delta <= 0) delta = 0x1000;
            address = IntPtr.Add(mbi.BaseAddress, (int)delta);

            if (mbi.State == MEM_FREE)
                address = IntPtr.Add(mbi.BaseAddress, 0x10000);

            if ((ulong)address.ToInt64() >= 0x7FFFFFFF || address.ToInt64() < 0)
                break;
        }

        return regions;
    }

    private List<ThreadRecord> EnumerateThreads(int processId, CancellationToken ct)
    {
        var records = new List<ThreadRecord>();

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return records;

        try
        {
            var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
            if (Thread32First(snapshot, ref te))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();

                    if ((int)te.th32OwnerProcessID == processId)
                    {
                        var record = new ThreadRecord
                        {
                            ThreadId = te.th32ThreadID,
                        };

                        var hThread = OpenThread(THREAD_QUERY_INFORMATION, false, (int)te.th32ThreadID);
                        if (hThread != IntPtr.Zero)
                        {
                            try
                            {
                                var buf = Marshal.AllocHGlobal(IntPtr.Size);
                                try
                                {
                                    if (NtQueryInformationThread(hThread, ThreadQuerySetWin32StartAddress, buf, IntPtr.Size, out _) == 0)
                                    {
                                        record.StartAddress = (ulong)Marshal.ReadIntPtr(buf).ToInt64();
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
                        }

                        records.Add(record);
                    }
                } while (Thread32Next(snapshot, ref te));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return records;
    }

    private void HashImageCodeSections(IntPtr hProcess, BaselineSnapshot snapshot, CancellationToken ct)
    {
        foreach (var region in snapshot.Regions)
        {
            ct.ThrowIfCancellationRequested();

            if (region.Type != MEM_IMAGE || region.RegionSize <= 0)
                continue;

            var isExecutable = (region.Protect & 0xF0) != 0;
            if (!isExecutable)
                continue;

            var moduleName = snapshot.Modules.ResolveModule(region.BaseAddress);
            region.ModuleName = moduleName;

            var hash = ComputeRegionHash(hProcess, new IntPtr((long)region.BaseAddress), Math.Min(region.RegionSize, 65536));
            region.CodeHash = hash;

            var moduleEntry = snapshot.Modules.Entries.FirstOrDefault(e => e.BaseAddress == region.BaseAddress);
            if (moduleEntry != null)
            {
                moduleEntry.CodeHash = hash;
            }
        }
    }

    private static ulong ComputeRegionHash(IntPtr hProcess, IntPtr baseAddress, long maxSize)
    {
        try
        {
            var size = Math.Min(maxSize, 65536);
            var buffer = new byte[size];
            if (ReadProcessMemory(hProcess, baseAddress, buffer, buffer.Length, out var bytesRead) && bytesRead > 0)
            {
                return XXHash64(buffer, bytesRead);
            }
        }
        catch { }
        return 0;
    }

    private static ulong XXHash64(byte[] data, int length)
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

    private static string? ReadUnicodeString(IntPtr hProcess, IntPtr ptr, int lengthBytes)
    {
        if (ptr == IntPtr.Zero || lengthBytes <= 0) return null;
        try
        {
            var bytes = new byte[lengthBytes];
            if (ReadProcessMemory(hProcess, ptr, bytes, bytes.Length, out var read) && read >= 2)
            {
                return Encoding.Unicode.GetString(bytes, 0, read).TrimEnd('\0');
            }
        }
        catch { }
        return null;
    }

    private static DateTime GetProcessStartTime(IntPtr hProcess)
    {
        if (GetProcessTimes(hProcess, out var creation, out _, out _, out _))
        {
            try { return DateTime.FromFileTime(creation); } catch { }
        }
        return DateTime.UtcNow;
    }
}
