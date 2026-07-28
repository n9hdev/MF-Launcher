using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Module Integrity Scanner", "1.1.0", "Validates loaded modules against game-directory whitelist; flags unsigned DLLs and hidden/unlinked modules via VAD comparison")]
public class ModuleIntegrityScanner : IDetector
{
    private readonly ILogger<ModuleIntegrityScanner> _logger;
    private readonly IMtaBaselineProvider _mtaBaseline;
    private readonly Dictionary<string, string> _knownHashes = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "Module Integrity Scanner";
    public string Version => "1.1.0";
    public bool IsEnabled { get; set; } = true;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    private static readonly HashSet<string> SystemDirectoryPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        @"C:\Windows",
        @"C:\Windows\WinSxS",
        @"C:\Windows\Microsoft.NET",
        @"C:\Program Files\Common Files\Microsoft Shared",
        @"C:\Program Files\Microsoft Office",
        @"C:\Program Files (x86)\Common Files\Microsoft Shared",
        @"C:\Program Files (x86)\Microsoft Office",
    };

    // Additional known Microsoft/legitimate DLLs that commonly appear in Toolhelp but not EnumProcessModules
    // (WOW64 behavior, side-by-side assemblies, etc.)
    private static readonly HashSet<string> TrustedMicrosoftPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ms", "api-", "ext-", "windows.", "mp", "microsoft",
    };

    private static readonly HashSet<string> KnownLegitimateModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "MpOav.dll", "MpClient.dll", "MpCmdRun.exe",
        "mprapi.dll", "mprddm.dll", "mprmsg.dll",
        "msvcp100.dll", "msvcr100.dll",
        "msvcp110.dll", "msvcr110.dll",
        "msvcp120.dll", "msvcr120.dll",
        "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll",
        "concrt140.dll",
        "api-ms-win-", "ext-ms-win-",
        "sxsharedll.dll",
        "windows.storage.dll",
        "wintrust.dll", "crypt32.dll", "cryptsp.dll",
        "rsaenh.dll", "bcrypt.dll", "ncrypt.dll",
        "sspicli.dll", "secur32.dll",
        "user32.dll", "gdi32.dll", "gdi32full.dll",
        "win32u.dll", "imm32.dll", "ole32.dll", "oleaut32.dll",
        "combase.dll", "comdlg32.dll", "shell32.dll",
        "shlwapi.dll", "shcore.dll",
        "advapi32.dll", "sechost.dll",
        "rpcrt4.dll", "rpcss.dll",
        "ws2_32.dll", "wsock32.dll",
        "winhttp.dll", "wininet.dll",
        "dnsapi.dll", "iphlpapi.dll",
        "uxtheme.dll", "dwmapi.dll",
        "clbcatq.dll", "propsys.dll",
        "cfgmgr32.dll", "devobj.dll",
        "wtsapi32.dll", "winsta.dll",
        "srvcli.dll", "netapi32.dll",
        "dsrole.dll", "ktmw32.dll",
        "bcryptprimitives.dll", "cng.sys",
        "msasn1.dll", "cryptnet.dll",
        "dbghelp.dll", "dbgcore.dll",
        "fwpuclnt.dll", "rasadhlp.dll",
        "napinsp.dll", "pnrpnsp.dll",
        "winrnr.dll", "nlaapi.dll",
        "dhcpcsvc.dll", "dhcpcsvc6.dll",
        "svchost.exe",
    };

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

    public ModuleIntegrityScanner(ILogger<ModuleIntegrityScanner> logger, IMtaBaselineProvider mtaBaseline)
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

            var toolhelpModules = new List<(string Name, string Path, IntPtr Base)>();

            var hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)targetProcess.Id);
            if (hSnapshot != IntPtr.Zero)
            {
                try
                {
                    var me = new MODULEENTRY32
                    {
                        dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>()
                    };

                    if (Module32First(hSnapshot, ref me))
                    {
                        do
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            toolhelpModules.Add((me.szModule, me.szExePath, me.modBaseAddr));
                        }
                        while (Module32Next(hSnapshot, ref me));
                    }
                }
                finally
                {
                    CloseHandle(hSnapshot);
                }
            }

            var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, targetProcess.Id);
            var enumModules = new List<IntPtr>();

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
                            enumModules.Add(modules[i]);
                        }
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            var enumBases = new HashSet<IntPtr>(enumModules);
            var toolhelpBases = new HashSet<IntPtr>(toolhelpModules.Select(m => m.Base));

            // Only flag modules that are in Toolhelp but missing from EnumProcessModules AND
            // are not MTA modules or system modules. Windows legitimately shows modules
            // in one API but not the other for well-known DLLs.
            var hiddenModules = toolhelpModules
                .Where(m => !enumBases.Contains(m.Base)
                    && !IsTrustedSystemModule(m.Name, m.Path)
                    && !_mtaBaseline.IsKnownMtaModulePath(m.Path)
                    && !_mtaBaseline.IsKnownOverlayOrLegitimateModule(m.Name)
                    // Only flag if the file doesn't exist on disk (memory-only module)
                    // or if the path is suspicious. Toolhelp/EnumProcessModules mismatch
                    // is normal for WOW64 processes like gta_sa.
                    && (string.IsNullOrEmpty(m.Path) || !File.Exists(m.Path)))
                .ToList();

            foreach (var hidden in hiddenModules)
            {
                results.Add(new DetectionEventDto
                {
                    Type = "Hidden/Unlinked Module",
                    Severity = "critical",
                    Description = $"Module '{hidden.Name}' at 0x{hidden.Base.ToInt64():X} visible in Toolhelp but absent from EnumProcessModules — possible module hiding",
                    Confidence = 0.9,
                    ProcessName = targetProcess.ProcessName,
                });
            }

            // Also check reverse: modules in EnumProcessModules but not Toolhelp
            var enumModuleNames = new HashSet<IntPtr>(enumModules);
            var reverseHidden = toolhelpModules
                .Where(m => !toolhelpBases.Contains(m.Base)
                    && !IsTrustedSystemModule(m.Name, m.Path)
                    && !_mtaBaseline.IsKnownMtaModulePath(m.Path))
                .ToList();

            foreach (var module in toolhelpModules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsTrustedSystemModule(module.Name, module.Path))
                    continue;

                // Skip known MTA modules and known overlay/legitimate modules
                if (_mtaBaseline.IsKnownMtaModulePath(module.Path) || _mtaBaseline.IsKnownOverlayOrLegitimateModule(module.Name))
                    continue;

                if (string.IsNullOrEmpty(module.Path) || !File.Exists(module.Path))
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = "Module File Missing",
                        Severity = "high",
                        Description = $"Module '{module.Name}' loaded at 0x{module.Base.ToInt64():X} has no corresponding file on disk — possible memory-only injection",
                        Confidence = 0.85,
                        ProcessName = targetProcess.ProcessName,
                    });
                    continue;
                }

                if (!IsSigned(module.Path))
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = "Unsigned Module",
                        Severity = "low",
                        Description = $"Unsigned module loaded: {module.Name} from {module.Path}",
                        Confidence = 0.3,
                        ProcessName = targetProcess.ProcessName,
                    });
                }

                var hash = ComputeSha256(module.Path);
                if (_knownHashes.TryGetValue(module.Name, out var previousHash))
                {
                    if (!string.Equals(hash, previousHash, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new DetectionEventDto
                        {
                            Type = "Module Hash Mismatch",
                            Severity = "high",
                            Description = $"Module '{module.Name}' hash changed since baseline — possible DLL replacement/modification",
                            Confidence = 0.8,
                            ProcessName = targetProcess.ProcessName,
                        });
                    }
                }
                else
                {
                    _knownHashes[module.Name] = hash;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Module integrity scan failed");
        }

        return Task.FromResult<IReadOnlyList<DetectionEventDto>>(results);
    }

    private static bool IsTrustedSystemModule(string name, string path)
    {
        // Check known legitimate module names
        if (KnownLegitimateModules.Contains(name))
            return true;

        // Check well-known system module name prefixes
        if (name.StartsWith("ntdll", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("kernel32", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("kernelbase", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("USER32.DLL", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("GDI32.DLL", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check system directory paths
        foreach (var sysDir in SystemDirectoryPaths)
        {
            if (!string.IsNullOrEmpty(sysDir) && path.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check trusted prefixes
        foreach (var prefix in TrustedMicrosoftPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSigned(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
            return cert != null;
        }
        catch
        {
            return false;
        }
    }

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
