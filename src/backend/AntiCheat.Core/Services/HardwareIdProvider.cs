using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Services;

public class HardwareIdProvider : IHardwareIdProvider
{
    private readonly ILogger<HardwareIdProvider> _logger;
    private string? _cachedHwid;
    private static readonly string HwidFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AntiCheat", "hwid.dat");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        StringBuilder lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder lpFileSystemNameBuffer,
        int nFileSystemNameSize);

    public HardwareIdProvider(ILogger<HardwareIdProvider> logger)
    {
        _logger = logger;
    }

    public string GetHardwareId()
    {
        if (_cachedHwid != null) return _cachedHwid;

        // Read from persistent file cache first (ensures same HWID across contexts)
        var fromFile = ReadHwidFromFile();
        if (fromFile != null)
        {
            _cachedHwid = fromFile;
            return _cachedHwid;
        }

        try
        {
            var fingerprint = GetCombinedHardwareId();
            if (string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Contains("unknown"))
            {
                _logger.LogWarning("Failed to get valid hardware ID");
                _cachedHwid = "unknown-hwid";
                return _cachedHwid;
            }

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(fingerprint);
            var hash = sha256.ComputeHash(bytes);
            var base64 = Convert.ToBase64String(hash)
                .Replace("=", "")
                .Replace("/", "#")
                .Replace("+", "@");

            _cachedHwid = base64;

            // Persist to DPAPI-encrypted file so the same HWID is used by both Service and app
            WriteHwidToFile(_cachedHwid);
            // Also write plaintext for the Dashboard (Electron) to read and link to account
            WritePlaintextHwid(_cachedHwid);

            return _cachedHwid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash HWID");
            _cachedHwid = "unknown-hwid";
            return _cachedHwid;
        }
    }

    public HardwareFingerprint GetHardwareFingerprint()
    {
        return new HardwareFingerprint
        {
            CpuId = GetWmiValue("Win32_Processor", "ProcessorId") ?? "no-cpu",
            GpuId = GetWmiValue("Win32_VideoController", "PNPDeviceID") ?? "no-gpu",
            DiskId = GetDiskSerial() ?? GetVolumeSerial() ?? "no-disk",
            OsVersion = Environment.OSVersion.VersionString,
            MachineName = Environment.MachineName,
        };
    }

    private string? GetCombinedHardwareId()
    {
        var cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
        var gpuId = GetWmiValue("Win32_VideoController", "PNPDeviceID");
        var diskId = GetDiskSerial() ?? GetVolumeSerial();

        cpuId = string.IsNullOrWhiteSpace(cpuId) ? "no-cpu" : cpuId;
        gpuId = string.IsNullOrWhiteSpace(gpuId) ? "no-gpu" : gpuId;
        diskId = string.IsNullOrWhiteSpace(diskId) ? "no-disk" : diskId;

        return $"{cpuId}-{gpuId}-{diskId}";
    }

    /// <summary>Reads disk serial from WMI (accessible from user context).</summary>
    private static string? GetDiskSerial()
    {
        // Win32_DiskDrive works in both SYSTEM and user contexts
        var serial = GetWmiValue("Win32_DiskDrive", "SerialNumber");
        if (!string.IsNullOrWhiteSpace(serial)) return serial.Trim();

        // Fallback to Win32_PhysicalMedia (requires admin, but try anyway)
        serial = GetWmiValue("Win32_PhysicalMedia", "SerialNumber");
        if (!string.IsNullOrWhiteSpace(serial)) return serial.Trim();

        return null;
    }

    /// <summary>Gets volume serial number of system drive (always available, no elevation needed).</summary>
    private static string? GetVolumeSerial()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var volumeName = new StringBuilder(256);
            var fileSystemName = new StringBuilder(256);

            if (GetVolumeInformation(root, volumeName, volumeName.Capacity,
                    out var serial, out _, out _, fileSystemName, fileSystemName.Capacity))
            {
                return $"VOL{serial:X8}";
            }
        }
        catch
        {
        }
        return null;
    }

    private string? ReadHwidFromFile()
    {
        try
        {
            if (File.Exists(HwidFilePath))
            {
                var encrypted = File.ReadAllBytes(HwidFilePath);
                if (encrypted.Length == 0) return null;

                var decryptedBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
                var content = Encoding.UTF8.GetString(decryptedBytes).Trim();
                if (!string.IsNullOrWhiteSpace(content) && content != "unknown-hwid")
                    return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not read HWID from cache file (tampered or wrong machine)");
        }
        return null;
    }

    private void WriteHwidToFile(string hwid)
    {
        try
        {
            var dir = Path.GetDirectoryName(HwidFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var bytes = Encoding.UTF8.GetBytes(hwid);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(HwidFilePath, encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not persist HWID to cache file");
        }
    }

    private static void WritePlaintextHwid(string hwid)
    {
        try
        {
            var dir = Path.GetDirectoryName(HwidFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var plainPath = Path.Combine(Path.GetDirectoryName(HwidFilePath)!, "hwid.txt");
            File.WriteAllText(plainPath, hwid);
        }
        catch
        {
        }
    }

    private static string? GetWmiValue(string wmiClass, string property)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT {property} FROM {wmiClass}");
            foreach (var item in searcher.Get())
            {
                var value = item[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WMI query failed for {wmiClass}.{property}: {ex.Message}");
        }
        return null;
    }
}
