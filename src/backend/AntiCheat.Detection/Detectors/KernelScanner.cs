using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Kernel Scanner", "1.0.0", "Enumerates running kernel drivers via P/Invoke and checks against known cheat and unsigned drivers")]
public class KernelScanner : IDetector
{
    private readonly ILogger<KernelScanner> _logger;
    private readonly IWhitelistProvider _whitelist;

    private static readonly HashSet<string> KnownCheatDrivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "kprocesshacker.sys", "pchunter.sys", "cheatdrv.sys",
        "unknown_driver.sys",
    };

    private static readonly HashSet<string> TrustedDrivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ntoskrnl.exe", "hal.dll", "kernal32.dll", "win32k.sys",
        "dxgkrnl.sys", "dxgmms2.sys", "nvlddmkm.sys", "igfx.sys",
        "atikmdag.sys", "usbhub.sys", "cdrom.sys", "ntfs.sys",
    };

    private static readonly HashSet<string> KnownSafeDrivers = new(StringComparer.OrdinalIgnoreCase)
    {
        "vgk.sys", "FairplayKD.sys", "EasyAntiCheat.sys", "EAC.sys",
        "BEDaisy.sys", "mhyprot2.sys", "mhyprot3.sys",
        "GameGuard.sys", "ggc.sys", "npggNT.des", "npfgg.sys",
        "nprotect.sys", "xigncode.sys", "x3.xem",
        "acdclient.sys", "tenio.sys", "tesx.sys", "tesx3.sys",
        "rsdt.sys", "rsdte.sys", "rsdto.sys",
        "kmod.sys", "kmod6.sys",
        "pci.sys", "cng.sys", "CI.dll", "FLTMGR.SYS",
        "ksecdd.sys", "KSecPkg.sys", "BOOTVID.dll", "CLFS.SYS",
        "ntosext.sys", "WDFLDR.SYS", "tm.sys", "mcupdate_GenuineIntel.dll",
        "kd.dll", "kdnic.sys", "hal.dll", "halacpi.dll",
        "ntoskrnl.exe", "ntkrnlmp.exe", "ntkrnlpa.exe", "ntkralmp.exe",
        "win32k.sys", "win32kfull.sys", "win32kbase.sys",
        "dxgkrnl.sys", "dxgmms2.sys", "dxgmms1.sys",
        "ntfs.sys", "usbhub.sys", "cdrom.sys", "ACPI.sys",
        "Wdf01000.sys", "tcpip.sys", "volsnap.sys",
        "storport.sys", "stornvme.sys", "storahci.sys",
        "partmgr.sys", "disk.sys", "Classpnp.sys",
        "fvevol.sys", "mountmgr.sys", "volmgr.sys", "volmgrx.sys",
        "afd.sys", "tdx.sys", "ndis.sys", "NetBT.sys",
        "bowser.sys", "mrxsmb.sys", "mrxsmb20.sys", "srv2.sys",
        "srvnet.sys", "HTTP.sys", "rdbss.sys",
        "usbehci.sys", "usbohci.sys", "usbuhci.sys", "USBXHCI.sys",
        "usbccgp.sys", "usbhub.sys", "HDAudBus.sys", "HdAudio.sys",
        "i8042prt.sys", "kbdclass.sys", "kbdhid.sys",
        "mouclass.sys", "mouhid.sys", "monitor.sys",
        "serenum.sys", "serial.sys", "sermouse.sys",
        "vmci.sys", "vsock.sys", "vmbus.sys", "VMBusHID.sys",
        "vmgid.sys", "vmkbd.sys", "vmsp.sys", "vmstorfl.sys",
        "vmsvsmp.sys", "vmusbmouse.sys",
        "PSHED.dll", "pcw.sys", "mssecflt.sys",
        "spaceport.sys", "uefi.sys", "wd.sys",
        "werkernel.sys", "wpprecorder.sys",
        "message.sys", "msquic.sys",
    };

    private static readonly HashSet<string> TrustedPublisherPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Microsoft Corporation",
        "Microsoft Windows", "Microsoft Windows Component Publisher",
        "Microsoft Windows Hardware Compatibility Publisher",
        "Windows Hardware Compatibility",
        "Riot Games", "Vanguard",
        "Fairplay", "Fairplay Technologies",
        "Easy Anti-Cheat", "Epic Games",
        "BattleEye", "BattlEye",
        "miHoYo", "Hoyoverse",
        "AhnLab", "nProtect",
        "Nexon", "GameGuard",
        "Wellbia", "XIGNCODE",
        "Tencent", "Tencent Technology",
        "Valve", "Valve Corporation",
    };

    private static string? _systemDriversPath;
    private static string? _systemRoot;

    public string Name => "Kernel Scanner";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public KernelScanner(ILogger<KernelScanner> logger, IWhitelistProvider whitelist)
    {
        _logger = logger;
        _whitelist = whitelist;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        if (!OperatingSystem.IsWindows())
            return results;

        try
        {
            var drivers = EnumerateKernelDrivers();
            if (drivers.Count == 0)
            {
                _logger.LogWarning("No kernel drivers returned from enumeration");
                return results;
            }

            foreach (var driver in drivers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = driver.Name;
                var path = driver.Path;
                var displayName = string.IsNullOrEmpty(driver.DisplayName) ? driver.Name : driver.DisplayName;

                if (string.IsNullOrEmpty(name))
                    continue;

                var fileName = path.Split('\\').LastOrDefault() ?? name;

                var win32Path = ConvertNtPathToWin32(path);
                if (!string.IsNullOrEmpty(win32Path))
                    path = win32Path;

                if (KnownCheatDrivers.Contains(fileName))
                {
                    results.Add(new DetectionEventDto
                    {
                        Type = "Known Cheat Driver",
                        Severity = "critical",
                        Description = $"Known malicious kernel driver running: {displayName} ({fileName})",
                        Confidence = 0.98,
                    });
                    continue;
                }

                if (KnownSafeDrivers.Contains(fileName) || TrustedDrivers.Contains(fileName))
                    continue;

                if (fileName.StartsWith("ms"))
                    continue;

                if (IsSystemDriver(path))
                    continue;

                if (await _whitelist.IsDriverWhitelistedAsync(fileName, path, cancellationToken))
                    continue;

                try
                {
                    var cert = GetFileCertificate(path);
                    if (cert != null)
                    {
                        if (IsTrustedPublisher(cert))
                            continue;

                        results.Add(new DetectionEventDto
                        {
                            Type = "Untrusted Publisher",
                            Severity = "medium",
                            Description = $"Driver signed by untrusted publisher '{cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false)}': {displayName} ({fileName})",
                            Confidence = 0.5,
                        });
                        continue;
                    }
                }
                catch (Exception exCert)
                {
                    _logger.LogTrace(exCert, "Failed to check certificate for: {Path}", path);
                }

                if (IsSystemFilePath(path))
                    continue;

                results.Add(new DetectionEventDto
                {
                    Type = "Unsigned Driver",
                    Severity = "medium",
                    Description = $"Unsigned kernel driver: {displayName} ({fileName})",
                    Confidence = 0.6,
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel scan failed");
        }

        return results;
    }

    private const int MaxDriverPath = 260;

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumDeviceDrivers(
        nint[] lpImageBases,
        int cb,
        out int lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int GetDeviceDriverBaseName(
        nint ImageBase,
        StringBuilder lpFileName,
        int nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int GetDeviceDriverFileName(
        nint ImageBase,
        StringBuilder lpFileName,
        int nSize);

    private readonly struct KernelDriverInfo
    {
        public readonly string Name;
        public readonly string Path;
        public readonly string DisplayName;

        public KernelDriverInfo(string name, string path, string displayName)
        {
            Name = name;
            Path = path;
            DisplayName = displayName;
        }
    }

    private static List<KernelDriverInfo> EnumerateKernelDrivers()
    {
        var result = new List<KernelDriverInfo>();

        const int initialSize = 1024;
        var images = new nint[initialSize];
        var ptrSize = IntPtr.Size;

        if (!EnumDeviceDrivers(images, initialSize * ptrSize, out var neededBytes))
        {
            return result;
        }

        var count = neededBytes / ptrSize;

        for (var i = 0; i < count && i < initialSize; i++)
        {
            var baseAddr = images[i];
            if (baseAddr == nint.Zero)
                continue;

            var nameBuilder = new StringBuilder(MaxDriverPath);
            if (GetDeviceDriverBaseName(baseAddr, nameBuilder, nameBuilder.Capacity) <= 0)
                continue;

            var pathBuilder = new StringBuilder(MaxDriverPath);
            GetDeviceDriverFileName(baseAddr, pathBuilder, pathBuilder.Capacity);

            var name = nameBuilder.ToString();
            var path = pathBuilder.ToString();

            result.Add(new KernelDriverInfo(name, path, name));
        }

        return result;
    }

    private static string? ConvertNtPathToWin32(string ntPath)
    {
        if (string.IsNullOrEmpty(ntPath))
            return null;

        _systemRoot ??= Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";

        const string sysRootPrefix = "\\SystemRoot\\";
        if (ntPath.StartsWith(sysRootPrefix, StringComparison.OrdinalIgnoreCase))
            return _systemRoot + "\\" + ntPath.Substring(sysRootPrefix.Length);

        const string objPrefix = "\\??\\";
        if (ntPath.StartsWith(objPrefix, StringComparison.OrdinalIgnoreCase))
            return ntPath.Substring(objPrefix.Length);

        const string devicePrefix = "\\Device\\";
        if (ntPath.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var afterDevice = ntPath.Substring(devicePrefix.Length);
            var harddiskEnd = afterDevice.IndexOf('\\');
            if (harddiskEnd > 0)
            {
                var devicePart = afterDevice.Substring(0, harddiskEnd);
                foreach (var driveLetter in "CDEFGHIJKLMNOPQRSTUVWXYZ")
                {
                    var driveStr = $"{driveLetter}:";
                    var targetPath = $"\\Device\\{driveStr}\\";
                    var actual = GetDosDeviceMapping(driveStr);
                    if (actual != null && actual.Equals($"\\Device\\{devicePart}", StringComparison.OrdinalIgnoreCase))
                        return driveStr + "\\" + afterDevice.Substring(harddiskEnd + 1);
                }
            }
        }

        return null;
    }

    private static string? GetDosDeviceMapping(string dosDevice)
    {
        try
        {
            var sb = new StringBuilder(256);
            if (QueryDosDevice(dosDevice, sb, sb.Capacity) > 0)
                return sb.ToString();
        }
        catch { }
        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

    private static bool IsSystemDriver(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.StartsWith("\\SystemRoot\\", StringComparison.OrdinalIgnoreCase))
            return true;

        _systemDriversPath ??= Path.Combine(
            Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows",
            "System32", "drivers");

        if (path.StartsWith(_systemDriversPath, StringComparison.OrdinalIgnoreCase))
            return true;

        var sysRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
        if (path.StartsWith(Path.Combine(sysRoot, "System32"), StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(Path.Combine(sysRoot, "SysWOW64"), StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(Path.Combine(sysRoot, "WinSxS"), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsSystemFilePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        _systemRoot ??= Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";

        return path.StartsWith(Path.Combine(_systemRoot, "System32"), StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(Path.Combine(_systemRoot, "SysWOW64"), StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(Path.Combine(_systemRoot, "WinSxS"), StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("\\SystemRoot\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrustedPublisher(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
    {
        try
        {
            var subject = cert.Subject;
            foreach (var pattern in TrustedPublisherPatterns)
            {
                if (subject.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2? GetFileCertificate(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            return (System.Security.Cryptography.X509Certificates.X509Certificate2)System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
        }
        catch
        {
            return null;
        }
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }
}
