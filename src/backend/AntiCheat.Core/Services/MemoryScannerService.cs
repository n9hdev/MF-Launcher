using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class MemoryScannerService : IMemoryScannerService
{
    private readonly ILogger<MemoryScannerService> _logger;
    private readonly IWhitelistProvider _whitelist;
    private readonly IMtaBaselineProvider _mtaBaseline;

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_PRIVATE = 0x20000;
    private const uint MEM_IMAGE = 0x1000000;

    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_READ = 0x20;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    private const uint PAGE_GUARD = 0x100;

    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPTHREAD = 0x00000004;
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    private const uint PROCESS_BASIC_INFO_CLASS = 0;

    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_FREE = 0x10000;

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO_EX lpmodinfo, uint cb);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO_EX
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

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

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int ProcessInformationClass, out PROCESS_BASIC_INFORMATION pbi, int ProcessInformationLength, out int ReturnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationThread(IntPtr hThread, int ThreadInformationClass, IntPtr ThreadInformation, int ThreadInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);

    private const uint THREAD_QUERY_INFORMATION = 0x0040;
    private const int ThreadQuerySetWin32StartAddress = 0x09;

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
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PEB_LDR_DATA
    {
        public uint Length;
        public byte Initialized;
        public IntPtr SsHandle;
        public IntPtr InLoadOrderModuleList;
        public IntPtr InMemoryOrderModuleList;
        public IntPtr InInitializationOrderModuleList;
        public IntPtr EntryInProgress;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LDR_DATA_TABLE_ENTRY
    {
        public LIST_ENTRY InLoadOrderLinks;
        public LIST_ENTRY InMemoryOrderLinks;
        public LIST_ENTRY InInitializationOrderLinks;
        public IntPtr DllBase;
        public IntPtr EntryPoint;
        public uint SizeOfImage;
        public UNICODE_STRING FullDllName;
        public UNICODE_STRING BaseDllName;
        // ... remaining fields not needed for our scan
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LIST_ENTRY
    {
        public IntPtr Flink;
        public IntPtr Blink;
    }

    private const int MAX_SCAN_ADDRESS = 0x7FFFFFFF;

    public MemoryScannerService(
        ILogger<MemoryScannerService> logger,
        IWhitelistProvider whitelist,
        IMtaBaselineProvider mtaBaseline)
    {
        _logger = logger;
        _whitelist = whitelist;
        _mtaBaseline = mtaBaseline;
    }

    public async Task<IReadOnlyList<MemoryEvidence>> ScanAsync(CancellationToken ct = default)
    {
        var evidence = new List<MemoryEvidence>();

        try
        {
            var gameProcess = Process.GetProcessesByName("gta_sa").FirstOrDefault()
                          ?? Process.GetProcessesByName("MTA").FirstOrDefault();

            if (gameProcess == null)
                return evidence;

            var pid = gameProcess.Id;

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                _logger.LogWarning("Cannot open game process (PID {Pid}) for memory scanning", pid);
                return evidence;
            }

            try
            {
                var regionEvidence = ScanMemoryRegions(hProcess, pid, gameProcess.ProcessName, ct);
                evidence.AddRange(regionEvidence);

                var moduleEvidence = await ScanModules(hProcess, pid, gameProcess.ProcessName, ct);
                evidence.AddRange(moduleEvidence);

                var threadEvidence = await ScanThreads(pid, gameProcess.ProcessName, ct);
                evidence.AddRange(threadEvidence);

                var hookEvidence = ScanInlineHooks(hProcess, pid, gameProcess.ProcessName, ct);
                evidence.AddRange(hookEvidence);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory scanner scan failed");
        }

        return evidence;
    }

    private List<MemoryEvidence> ScanMemoryRegions(IntPtr hProcess, int pid, string processName, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();
        var address = IntPtr.Zero;
        var rwxPrivateCount = 0;
        var executablePrivateCount = 0;

        // Build module address ranges from EnumProcessModules for accurate filtering
        var moduleRanges = new List<(long Start, long End, string Name)>();
        var modules = new IntPtr[1024];
        if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
        {
            var count = (int)(needed / (uint)IntPtr.Size);
            for (var i = 0; i < count; i++)
            {
                if (GetModuleInformation(hProcess, modules[i], out var modInfo, (uint)Marshal.SizeOf<MODULEINFO_EX>()))
                {
                    var sb = new System.Text.StringBuilder(256);
                    GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                    var modName = sb.ToString();
                    moduleRanges.Add((modInfo.lpBaseOfDll.ToInt64(), modInfo.lpBaseOfDll.ToInt64() + modInfo.SizeOfImage, modName));
                }
            }
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!VirtualQueryEx(hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                break;

            if (mbi.State == MEM_COMMIT && mbi.RegionSize.ToInt64() > 0)
            {
                var baseAddr = mbi.BaseAddress.ToInt64();
                var isExecutable = (mbi.Protect & 0xF0) != 0;
                var isRWX = mbi.Protect == PAGE_EXECUTE_READWRITE;
                var isPrivate = mbi.Type == MEM_PRIVATE;
                var isImage = mbi.Type == MEM_IMAGE;
                var size = mbi.RegionSize.ToInt64();

                // Check if address falls within a known loaded module range
                bool inKnownModule = false;
                foreach (var mod in moduleRanges)
                {
                    if (baseAddr >= mod.Start && baseAddr < mod.End)
                    {
                        inKnownModule = true;
                        break;
                    }
                }

                if (isRWX && isPrivate && !isImage && !inKnownModule)
                {
                    rwxPrivateCount++;
                    evidence.Add(new MemoryEvidence
                    {
                        EvidenceType = "RWX_Private_Memory",
                        Severity = rwxPrivateCount > 3 ? "high" : "medium",
                        Description = $"RWX private memory at 0x{baseAddr:X} size {size:N0} bytes — possible shellcode/injected code",
                        ProcessName = processName,
                        ProcessId = pid,
                        Address = baseAddr,
                        RegionSize = size,
                        Detail = $"Protect=PAGE_EXECUTE_READWRITE, Type=MEM_PRIVATE",
                        Tags = { "rwx", "private", "shellcode" },
                    });
                }
                else if (isExecutable && isPrivate && !isImage && !inKnownModule)
                {
                    executablePrivateCount++;
                    if (size <= 0x100000)
                    {
                        var buffer = new byte[Math.Min((int)size, 4096)];
                        if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead) && bytesRead > 2)
                        {
                            if (buffer[0] == 'M' && buffer[1] == 'Z')
                            {
                                evidence.Add(new MemoryEvidence
                                {
                                    EvidenceType = "PE_Header_Private_Memory",
                                    Severity = "critical",
                                    Description = $"PE header (MZ) found in private executable memory at 0x{baseAddr:X} — manual-map injection",
                                    ProcessName = processName,
                                    ProcessId = pid,
                                    Address = baseAddr,
                                    RegionSize = size,
                                    Detail = $"PE image mapped in non-image memory region",
                                    Tags = { "pe_header", "private", "manual_map" },
                                });
                            }
                        }
                    }
                }

                if (isRWX && isImage)
                {
                    evidence.Add(new MemoryEvidence
                    {
                        EvidenceType = "RWX_Image_Memory",
                        Severity = "medium",
                        Description = $"RWX image memory at 0x{baseAddr:X} — possible modified code section",
                        ProcessName = processName,
                        ProcessId = pid,
                        Address = baseAddr,
                        RegionSize = size,
                        Tags = { "rwx", "image" },
                    });
                }
            }

            var delta = mbi.RegionSize.ToInt64();
            if (delta <= 0) delta = 0x1000;
            address = IntPtr.Add(mbi.BaseAddress, (int)delta);

            if (mbi.State == MEM_FREE)
                address = IntPtr.Add(mbi.BaseAddress, 0x10000);

            if (address.ToInt64() >= MAX_SCAN_ADDRESS || address.ToInt64() < 0)
                break;
        }

        if (rwxPrivateCount > 10)
        {
            evidence.Add(new MemoryEvidence
            {
                EvidenceType = "Mass_RWX_Allocation",
                Severity = "medium",
                Description = $"Process has {rwxPrivateCount} RWX private memory regions — extensive code injection",
                ProcessName = processName,
                ProcessId = pid,
                Detail = $"{rwxPrivateCount} RWX regions found",
                Tags = { "rwx", "mass_allocation" },
            });
        }

        if (executablePrivateCount > 20)
        {
            evidence.Add(new MemoryEvidence
            {
                EvidenceType = "Mass_Executable_Private",
                Severity = "low",
                Description = $"Process has {executablePrivateCount} executable private memory regions — check for injection",
                ProcessName = processName,
                ProcessId = pid,
                Detail = $"{executablePrivateCount} executable private regions",
                Tags = { "executable", "private" },
            });
        }

        return evidence;
    }

    private async Task<List<MemoryEvidence>> ScanModules(IntPtr hProcess, int pid, string processName, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)pid);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return evidence;

        var knownModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };

            if (Module32First(snapshot, ref me))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();
                    knownModules.Add(me.szModule);

                    var modulePath = me.szExePath;
                    var moduleName = me.szModule;

                    if (modulePath.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (modulePath.StartsWith(@"C:\Program Files\MTA San Andreas", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip known game, MTA, and overlay modules
                    if (moduleName.Equals("gta_sa.exe", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.Equals("MTA.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (_mtaBaseline.IsKnownMtaModule(moduleName) || _mtaBaseline.IsKnownMtaModulePath(modulePath))
                        continue;

                    if (_mtaBaseline.IsKnownOverlayOrLegitimateModule(moduleName))
                        continue;

                    if (await _whitelist.IsProcessWhitelistedAsync(moduleName))
                        continue;

                    evidence.Add(new MemoryEvidence
                    {
                        EvidenceType = "NonStandard_Game_Module",
                        Severity = "low",
                        Description = $"Non-standard module loaded in game process: {moduleName}",
                        ProcessName = processName,
                        ProcessId = pid,
                        Address = me.modBaseAddr.ToInt64(),
                        RegionSize = me.modBaseSize,
                        Detail = $"ModulePath={modulePath}",
                        Tags = { "module", "non_standard" },
                    });
                } while (Module32Next(snapshot, ref me));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var hiddenModules = await DetectHiddenModules(hProcess, pid, processName, knownModules, ct);
        evidence.AddRange(hiddenModules);

        return evidence;
    }

    private async Task<List<MemoryEvidence>> DetectHiddenModules(IntPtr hProcess, int pid, string processName, HashSet<string> knownModules, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();

        try
        {
            if (NtQueryInformationProcess(hProcess, (int)PROCESS_BASIC_INFO_CLASS, out var pbi,
                Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _) != 0)
                return evidence;

            if (pbi.PebBaseAddress == IntPtr.Zero)
                return evidence;

            // PEB.Ldr pointer: offset 0x18 (x64) or 0x0C (x86), size = IntPtr.Size
            var ldrFieldOffset = IntPtr.Size == 8 ? 0x18 : 0x0C;
            var ldrPtrBytes = new byte[IntPtr.Size];
            if (!ReadProcessMemory(hProcess, pbi.PebBaseAddress + ldrFieldOffset, ldrPtrBytes, ldrPtrBytes.Length, out _))
                return evidence;

            IntPtr ldrAddr;
            if (IntPtr.Size == 8)
                ldrAddr = new IntPtr(BitConverter.ToInt64(ldrPtrBytes, 0));
            else
                ldrAddr = new IntPtr(BitConverter.ToInt32(ldrPtrBytes, 0));

            if (ldrAddr == IntPtr.Zero) return evidence;

            // InLoadOrderModuleList at PEB_LDR_DATA + 0x0C (both x86 and x64)
            var inLoadOrderModuleList = Marshal.PtrToStructure<LIST_ENTRY>(ldrAddr + 0x0C);

            var currentFlink = inLoadOrderModuleList.Flink;
            var listHead = inLoadOrderModuleList.Flink;

            var visited = 0;
            while (currentFlink != IntPtr.Zero && visited < 200)
            {
                ct.ThrowIfCancellationRequested();
                visited++;

                var entryBytes = new byte[Marshal.SizeOf<LDR_DATA_TABLE_ENTRY>()];
                if (!ReadProcessMemory(hProcess, currentFlink, entryBytes, entryBytes.Length, out _))
                    break;

                var dllBase = Marshal.ReadIntPtr(currentFlink + 0x18);
                var fullDllNameOffset = 0x30;

                var nameLength = Marshal.ReadInt16(currentFlink + fullDllNameOffset);
                var namePtr = Marshal.ReadIntPtr(currentFlink + fullDllNameOffset + 0x08);

                if (nameLength > 0 && namePtr != IntPtr.Zero)
                {
                    var nameBytes = new byte[nameLength];
                    if (ReadProcessMemory(hProcess, namePtr, nameBytes, nameBytes.Length, out _))
                    {
                        var moduleName = System.Text.Encoding.Unicode.GetString(nameBytes);
                        var fileName = Path.GetFileName(moduleName);

                        if (!string.IsNullOrWhiteSpace(fileName) && !knownModules.Contains(fileName))
                        {
                            if (await _whitelist.IsProcessWhitelistedAsync(fileName))
                            {
                                var linkBytes = new byte[IntPtr.Size * 2];
                                if (!ReadProcessMemory(hProcess, currentFlink, linkBytes, linkBytes.Length, out _))
                                    break;
                                currentFlink = Marshal.ReadIntPtr(currentFlink);
                                continue;
                            }

                            evidence.Add(new MemoryEvidence
                            {
                                EvidenceType = "Hidden_Module",
                                Severity = "critical",
                                Description = $"Hidden/unlinked module detected in PEB: {fileName}",
                                ProcessName = processName,
                                ProcessId = pid,
                                Address = dllBase.ToInt64(),
                                Detail = $"Module absent from Toolhelp32 snapshot but present in PEB loader data",
                                Tags = { "hidden_module", "peb", "unlinked" },
                            });
                        }
                    }
                }

                var nextLink = Marshal.ReadIntPtr(currentFlink);
                if (nextLink == currentFlink || nextLink == IntPtr.Zero)
                    break;
                currentFlink = nextLink;

                if (currentFlink == listHead)
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Hidden module detection failed for PID {Pid}", pid);
        }

        return evidence;
    }

    private async Task<List<MemoryEvidence>> ScanThreads(int pid, string processName, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return evidence;

        var gameThreads = new List<uint>();

        try
        {
            var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };

            if (Thread32First(snapshot, ref te))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();
                    if ((int)te.th32OwnerProcessID == pid)
                    {
                        gameThreads.Add(te.th32ThreadID);
                    }
                } while (Thread32Next(snapshot, ref te));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        if (gameThreads.Count > 300)
        {
            evidence.Add(new MemoryEvidence
            {
                EvidenceType = "Excessive_Thread_Count",
                Severity = "low",
                Description = $"Game process has {gameThreads.Count} threads — unusually high",
                ProcessName = processName,
                ProcessId = pid,
                Detail = $"{gameThreads.Count} threads",
                Tags = { "threads", "excessive" },
            });
        }

        var unknownThreads = await CheckThreadOwners(gameThreads, pid, processName, ct);
        evidence.AddRange(unknownThreads);

        return evidence;
    }

    private Task<List<MemoryEvidence>> CheckThreadOwners(List<uint> gameThreads, int pid, string processName, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();
        if (gameThreads.Count == 0) return Task.FromResult(evidence);

        // Use EnumProcessModules + GetModuleInformation (more reliable than Toolhelp)
        var knownModuleRanges = new List<(long Base, long End, string Name)>();
        var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                var modules = new IntPtr[1024];
                if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
                {
                    var count = (int)(needed / (uint)IntPtr.Size);
                    for (var i = 0; i < count; i++)
                    {
                        if (GetModuleInformation(hProcess, modules[i], out var modInfo, (uint)Marshal.SizeOf<MODULEINFO_EX>()))
                        {
                            var sb = new System.Text.StringBuilder(256);
                            GetModuleBaseName(hProcess, modules[i], sb, (uint)sb.Capacity);
                            knownModuleRanges.Add((modInfo.lpBaseOfDll.ToInt64(), modInfo.lpBaseOfDll.ToInt64() + modInfo.SizeOfImage, sb.ToString()));
                        }
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        // Fallback: if EnumProcessModules failed, use Toolhelp
        if (knownModuleRanges.Count == 0)
        {
            var modSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)pid);
            if (modSnapshot != IntPtr.Zero && modSnapshot != new IntPtr(-1))
            {
                try
                {
                    var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
                    if (Module32First(modSnapshot, ref me))
                    {
                        do
                        {
                            var baseAddr = me.modBaseAddr.ToInt64();
                            knownModuleRanges.Add((baseAddr, baseAddr + me.modBaseSize, me.szModule));
                        } while (Module32Next(modSnapshot, ref me));
                    }
                }
                finally
                {
                    CloseHandle(modSnapshot);
                }
            }
        }

        // Known Windows system DLL address ranges (WOW64 — 32-bit process on 64-bit Windows)
        // Most system DLLs load in the 0x6xxxxxxx-0x7FFFFFFF range for 32-bit processes
        // These are legitimate thread start address sources
        var knownSystemDllRanges = new List<(long Base, long End)>
        {
            (0x6F000000, 0x80000000), // System DLL range for WOW64 processes
        };

        foreach (var threadId in gameThreads)
        {
            ct.ThrowIfCancellationRequested();

            var hThread = OpenThread(THREAD_QUERY_INFORMATION, false, (int)threadId);
            if (hThread == IntPtr.Zero) continue;

            try
            {
                var startAddrBuf = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    var status = NtQueryInformationThread(hThread, ThreadQuerySetWin32StartAddress,
                        startAddrBuf, IntPtr.Size, out _);
                    if (status != 0) continue;

                    var startAddr = Marshal.ReadIntPtr(startAddrBuf).ToInt64();
                    if (startAddr == 0) continue;

                    var inKnownModule = knownModuleRanges.Any(r => startAddr >= r.Base && startAddr < r.End);
                    var inSystemRange = knownSystemDllRanges.Any(r => startAddr >= r.Base && startAddr < r.End);

                    // Only flag if the address is NOT in a known module AND NOT in a system DLL range
                    if (!inKnownModule && !inSystemRange)
                    {
                        evidence.Add(new MemoryEvidence
                        {
                            EvidenceType = "Process_With_Game_Threads",
                            Severity = "high",
                            Description = $"Thread 0x{threadId:X} in game process starts at unknown address 0x{startAddr:X} — possible remote thread injection",
                            ProcessName = processName,
                            ProcessId = pid,
                            Address = startAddr,
                            Detail = $"ThreadId=0x{threadId:X}, StartAddress=0x{startAddr:X}",
                            Tags = { "thread", "cross_process", "remote_thread" },
                        });
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(startAddrBuf);
                }
            }
            finally
            {
                CloseHandle(hThread);
            }
        }

        return Task.FromResult(evidence);
    }

    private List<MemoryEvidence> ScanInlineHooks(IntPtr hProcess, int pid, string processName, CancellationToken ct)
    {
        var evidence = new List<MemoryEvidence>();

        var knownFunctions = new Dictionary<string, (IntPtr Address, int Size)>
        {
            { "ntdll!NtOpenProcess", (GetRemoteProcAddress("ntdll.dll", "NtOpenProcess"), 32) },
            { "ntdll!NtWriteVirtualMemory", (GetRemoteProcAddress("ntdll.dll", "NtWriteVirtualMemory"), 32) },
            { "ntdll!NtReadVirtualMemory", (GetRemoteProcAddress("ntdll.dll", "NtReadVirtualMemory"), 32) },
            { "ntdll!NtProtectVirtualMemory", (GetRemoteProcAddress("ntdll.dll", "NtProtectVirtualMemory"), 32) },
            { "ntdll!NtCreateThreadEx", (GetRemoteProcAddress("ntdll.dll", "NtCreateThreadEx"), 32) },
            { "ntdll!NtAllocateVirtualMemory", (GetRemoteProcAddress("ntdll.dll", "NtAllocateVirtualMemory"), 32) },
            { "kernel32!OpenProcess", (GetRemoteProcAddress("kernel32.dll", "OpenProcess"), 16) },
            { "kernel32!WriteProcessMemory", (GetRemoteProcAddress("kernel32.dll", "WriteProcessMemory"), 16) },
            { "kernel32!CreateRemoteThread", (GetRemoteProcAddress("kernel32.dll", "CreateRemoteThread"), 16) },
            { "kernel32!VirtualAllocEx", (GetRemoteProcAddress("kernel32.dll", "VirtualAllocEx"), 16) },
            { "kernel32!VirtualProtectEx", (GetRemoteProcAddress("kernel32.dll", "VirtualProtectEx"), 16) },
        };

        foreach (var (name, (address, size)) in knownFunctions)
        {
            ct.ThrowIfCancellationRequested();

            if (address == IntPtr.Zero)
                continue;

            var buffer = new byte[size];
            if (!ReadProcessMemory(hProcess, address, buffer, buffer.Length, out var bytesRead) || bytesRead < 5)
                continue;

            if (buffer.Length >= 5)
            {
                var isJmpFar = buffer[0] == 0xE9;
                var isCallFar = buffer[0] == 0xE8;
                var isJmpIndirect = buffer[0] == 0xFF && buffer[1] == 0x25;
                var isPushRet = buffer[0] == 0x68 && buffer[4] == 0xC3;

                if (isJmpFar || isCallFar || isJmpIndirect || isPushRet)
                {
                    evidence.Add(new MemoryEvidence
                    {
                        EvidenceType = "Inline_Hook_Detected",
                        Severity = "critical",
                        Description = $"Inline hook at {name} — prologue modified ({DescribeHook(buffer)})",
                        ProcessName = processName,
                        ProcessId = pid,
                        Address = address.ToInt64(),
                        Detail = $"Function={name}, HookType={DescribeHook(buffer)}",
                        Tags = { "hook", "inline_hook", "detour" },
                    });
                }
            }
        }

        return evidence;
    }

    private static string DescribeHook(byte[] prologue)
    {
        if (prologue.Length < 5) return "unknown";
        if (prologue[0] == 0xE9) return $"JMP 0x{(BitConverter.ToInt32(prologue, 1) + 5):X}";
        if (prologue[0] == 0xE8) return $"CALL 0x{(BitConverter.ToInt32(prologue, 1) + 5):X}";
        if (prologue[0] == 0xFF && prologue[1] == 0x25) return "JMP [indirect]";
        if (prologue[0] == 0x68 && prologue[4] == 0xC3) return "PUSH/RET (JMP via stack)";
        return $"0x{prologue[0]:X2} 0x{prologue[1]:X2} 0x{prologue[2]:X2} ...";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr PInvokeGetProcAddress(IntPtr hModule, string lpProcName);

    private static IntPtr GetRemoteProcAddress(string dllName, string procName)
    {
        try
        {
            var modulePtr = GetModuleHandle(dllName);
            if (modulePtr == IntPtr.Zero)
            {
                modulePtr = LoadLibrary(dllName);
                if (modulePtr == IntPtr.Zero) return IntPtr.Zero;
            }

            return PInvokeGetProcAddress(modulePtr, procName);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
