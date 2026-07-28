using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class BehavioralMonitorService : IBehavioralMonitorService
{
    private readonly ILogger<BehavioralMonitorService> _logger;
    private readonly IWhitelistProvider _whitelist;
    private static readonly int ProcessTypeIndex = GetProcessTypeIndex();

    private const int STATUS_INFO_LENGTH_MISMATCH = -1073741820;
    private const int STATUS_SUCCESS = 0;
    private const int SystemHandleInformation = 16;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_DUP_HANDLE = 0x0040;
    private const uint DUPLICATE_SAME_ACCESS = 0x0002;
    private const uint TH32CS_SNAPTHREAD = 0x00000004;

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryObject(IntPtr Handle, int ObjectInformationClass, IntPtr ObjectInformation, int ObjectInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetProcessId(IntPtr Process);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern uint WinVerifyTrust(IntPtr hWnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWinTrustData);

    private static readonly Guid WinTrustActionGenericVerifyV2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
    {
        public ushort UniqueProcessId;
        public ushort CreatorBackTraceIndex;
        public byte ObjectTypeIndex;
        public byte HandleAttributes;
        public ushort HandleValue;
        public IntPtr ObjectPointer;
        public int GrantedAccess;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    private static int GetProcessTypeIndex()
    {
        try
        {
            var os = Environment.OSVersion.Version;
            if (os.Major >= 10) return 7;
            if (os.Major == 6 && os.Minor >= 2) return 7;
            return 5;
        }
        catch
        {
            return 7;
        }
    }

    public BehavioralMonitorService(
        ILogger<BehavioralMonitorService> logger,
        IWhitelistProvider whitelist)
    {
        _logger = logger;
        _whitelist = whitelist;
    }

    public async Task<List<EvidenceFact>> ScanAsync(CancellationToken ct = default)
    {
        var evidence = new List<EvidenceFact>();

        try
        {
            var gameProcess = Process.GetProcessesByName("gta_sa").FirstOrDefault()
                          ?? Process.GetProcessesByName("MTA").FirstOrDefault();

            if (gameProcess == null)
                return evidence;

            var gamePid = gameProcess.Id;

            var handleEvidence = await CheckProcessHandles(gamePid, ct);
            evidence.AddRange(handleEvidence);

            var moduleEvidence = await CheckUnsignedModules(gamePid, ct);
            evidence.AddRange(moduleEvidence);

            var threadEvidence = await CheckSuspiciousThreads(gamePid, ct);
            evidence.AddRange(threadEvidence);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Behavioral monitor scan failed");
        }

        return evidence;
    }

    private async Task<List<EvidenceFact>> CheckProcessHandles(int targetPid, CancellationToken ct)
    {
        var evidence = new List<EvidenceFact>();

        var bufferSize = 0x10000;
        var ptr = IntPtr.Zero;

        try
        {
            int returnLength;
            int status;

            do
            {
                ct.ThrowIfCancellationRequested();

                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);

                bufferSize *= 2;
                ptr = Marshal.AllocHGlobal(bufferSize);
                status = NtQuerySystemInformation(SystemHandleInformation, ptr, bufferSize, out returnLength);
            } while (status == STATUS_INFO_LENGTH_MISMATCH);

            if (status != STATUS_SUCCESS)
            {
                _logger.LogWarning("NtQuerySystemInformation failed: 0x{Status:X8}", status);
                return evidence;
            }

            var handleCount = Marshal.ReadInt32(ptr);
            var entrySize = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO>();

            for (var i = 0; i < handleCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entryPtr = ptr + sizeof(int) + i * entrySize;
                var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO>(entryPtr);

                if (entry.ObjectTypeIndex != ProcessTypeIndex)
                    continue;

                var sourcePid = (int)entry.UniqueProcessId;

                if (sourcePid == targetPid || sourcePid <= 4)
                    continue;

                var sourceProcessName = GetProcessNameSafe(sourcePid);

                if (string.IsNullOrWhiteSpace(sourceProcessName))
                    continue;

                if (await _whitelist.IsProcessWhitelistedAsync(sourceProcessName))
                    continue;

                IntPtr? duplicatedHandle = null;
                var sourceHandle = OpenProcess(PROCESS_DUP_HANDLE, false, sourcePid);
                if (sourceHandle == IntPtr.Zero)
                    continue;

                try
                {
                    var handleVal = new IntPtr(entry.HandleValue);
                    if (DuplicateHandle(sourceHandle, handleVal, GetCurrentProcessHandle(), out var dupHandle, 0, false, DUPLICATE_SAME_ACCESS))
                    {
                        duplicatedHandle = dupHandle;
                        var actualPid = GetProcessId(dupHandle);
                        if (actualPid == targetPid)
                        {
                            var accessStr = DescribeGrantedAccess(entry.GrantedAccess);

                            evidence.Add(new EvidenceFact
                            {
                                Category = "Behavioral",
                                Observation = "HandleToGameProcess",
                                Interpretation = $"Process {sourceProcessName} (PID {sourcePid}) has open handle to game process with access: {accessStr}",
                                IsAnomaly = entry.GrantedAccess >= 0x1FFFFF,
                                ProcessName = sourceProcessName,
                                ProcessId = sourcePid,
                                SourceComponent = "BehavioralMonitorService",
                                Tags = { "handle", "injection", "openprocess" },
                                Details = new()
                                {
                                    ["HandleValue"] = $"0x{entry.HandleValue:X4}",
                                    ["AccessMask"] = $"0x{entry.GrantedAccess:X8}",
                                    ["AccessFlags"] = accessStr,
                                    ["TargetPid"] = targetPid.ToString(),
                                },
                            });
                        }
                    }
                }
                finally
                {
                    if (duplicatedHandle.HasValue && duplicatedHandle.Value != IntPtr.Zero)
                        CloseHandle(duplicatedHandle.Value);
                    CloseHandle(sourceHandle);
                }
            }
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }

        return evidence;
    }

    private async Task<List<EvidenceFact>> CheckUnsignedModules(int targetPid, CancellationToken ct)
    {
        var evidence = new List<EvidenceFact>();

        var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, targetPid);
        if (hProcess == IntPtr.Zero)
            return evidence;

        try
        {
            var modules = new IntPtr[1024];
            if (EnumProcessModules(hProcess, modules, (uint)(IntPtr.Size * modules.Length), out var needed))
            {
                var moduleCount = (int)(needed / (uint)IntPtr.Size);

                for (var i = 0; i < moduleCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var sb = new StringBuilder(260);
                    if (GetModuleFileNameEx(hProcess, modules[i], sb, (uint)sb.Capacity) > 0)
                    {
                        var modulePath = sb.ToString();
                        var moduleName = Path.GetFileName(modulePath);

                        if (await _whitelist.IsProcessWhitelistedAsync(moduleName))
                            continue;

                        if (modulePath.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase) ||
                            modulePath.StartsWith(@"C:\Program Files\MTA San Andreas", StringComparison.OrdinalIgnoreCase) ||
                            modulePath.StartsWith(@"C:\Program Files (x86)\MTA San Andreas", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var signed = IsFileSigned(modulePath);
                        if (!signed && !moduleName.Equals("gta_sa.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            evidence.Add(new EvidenceFact
                            {
                                Category = "Behavioral",
                                Observation = "UnsignedModuleInGame",
                                Interpretation = $"Unsigned module {moduleName} loaded in game process",
                                IsAnomaly = true,
                                ProcessName = moduleName,
                                ProcessId = targetPid,
                                SourceComponent = "BehavioralMonitorService",
                                Tags = { "module", "unsigned", "sideloading" },
                                Details = new()
                                {
                                    ["ModulePath"] = modulePath,
                                    ["TargetPid"] = targetPid.ToString(),
                                },
                            });
                        }
                    }
                }
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }

        return evidence;
    }

    private Task<List<EvidenceFact>> CheckSuspiciousThreads(int targetPid, CancellationToken ct)
    {
        var evidence = new List<EvidenceFact>();

        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return Task.FromResult(evidence);

        var gameThreadCount = 0;

        try
        {
            var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };

            if (Thread32First(snapshot, ref te))
            {
                do
                {
                    ct.ThrowIfCancellationRequested();

                    if ((int)te.th32OwnerProcessID == targetPid)
                        gameThreadCount++;
                } while (Thread32Next(snapshot, ref te));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        if (gameThreadCount > 300)
        {
            evidence.Add(new EvidenceFact
            {
                Category = "Behavioral",
                Observation = "ExcessiveThreadCount",
                Interpretation = $"Game process has {gameThreadCount} threads — unusually high",
                IsAnomaly = false,
                ProcessName = "gta_sa",
                ProcessId = targetPid,
                SourceComponent = "BehavioralMonitorService",
                Tags = { "threads", "excessive" },
                Details = new()
                {
                    ["ThreadCount"] = gameThreadCount.ToString(),
                },
            });
        }

        return Task.FromResult(evidence);
    }

    private static bool IsFileSigned(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var wtd = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = 2,
                fdwRevocationChecks = 0,
                dwUnionChoice = 1,
                pFile = fileInfoPtr,
                dwStateAction = 0,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0x00000010,
                dwUIContext = 0,
            };

            var wtdPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
            Marshal.StructureToPtr(wtd, wtdPtr, false);

            try
            {
                var result = WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, wtdPtr);
                return result == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(wtdPtr);
                Marshal.FreeHGlobal(fileInfoPtr);
            }
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeGrantedAccess(int access)
    {
        var flags = new List<string>();
        if ((access & 0x0001) != 0) flags.Add("PROCESS_TERMINATE");
        if ((access & 0x0002) != 0) flags.Add("PROCESS_CREATE_THREAD");
        if ((access & 0x0004) != 0) flags.Add("PROCESS_SET_SESSIONID");
        if ((access & 0x0008) != 0) flags.Add("PROCESS_VM_OPERATION");
        if ((access & 0x0010) != 0) flags.Add("PROCESS_VM_READ");
        if ((access & 0x0020) != 0) flags.Add("PROCESS_VM_WRITE");
        if ((access & 0x0040) != 0) flags.Add("PROCESS_DUP_HANDLE");
        if ((access & 0x0080) != 0) flags.Add("PROCESS_CREATE_PROCESS");
        if ((access & 0x0100) != 0) flags.Add("PROCESS_SET_QUOTA");
        if ((access & 0x0200) != 0) flags.Add("PROCESS_SET_INFORMATION");
        if ((access & 0x0400) != 0) flags.Add("PROCESS_QUERY_INFORMATION");
        if ((access & 0x0800) != 0) flags.Add("PROCESS_SUSPEND_RESUME");
        if ((access & 0x1000) != 0) flags.Add("PROCESS_QUERY_LIMITED_INFORMATION");
        if ((access & 0x001F0000) != 0) flags.Add("STANDARD_RIGHTS_ALL");
        if ((access & 0x1FFFFF) == 0x1FFFFF) flags.Add("PROCESS_ALL_ACCESS");

        return flags.Count > 0 ? string.Join(" | ", flags) : $"0x{access:X8}";
    }

    private static IntPtr GetCurrentProcessHandle()
    {
        return new IntPtr(-1);
    }

    private static string GetProcessNameSafe(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            return proc.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
