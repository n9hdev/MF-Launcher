using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class MtasaSerialReader : IMtasaSerialReader
{
    private readonly ILogger<MtasaSerialReader> _logger;
    private const string RegistryPath = @"SOFTWARE\WOW6432Node\Multi Theft Auto: San Andreas All\1.6\Settings\general";
    private const string SerialValueName = "serial";
    private const string CacheChecksumValueName = "cachechecksum";

    public MtasaSerialReader(ILogger<MtasaSerialReader> logger)
    {
        _logger = logger;
    }

    public MtasaSerialData ReadSerial()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new MtasaSerialData { Found = false, ErrorMessage = "Registry access requires Windows" };
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
            if (key == null)
            {
                _logger.LogWarning("MTASA registry key not found: HKLM\\{Path}", RegistryPath);
                return new MtasaSerialData { Found = false, ErrorMessage = "MTASA registry key not found" };
            }

            var serial = key.GetValue(SerialValueName)?.ToString();
            var cacheChecksum = key.GetValue(CacheChecksumValueName)?.ToString();

            if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(cacheChecksum))
            {
                _logger.LogWarning("MTASA serial or cache checksum not found in registry");
                return new MtasaSerialData { Found = false, ErrorMessage = "Serial or CacheChecksum not found" };
            }

            _logger.LogDebug("MTASA serial read successfully");
            return new MtasaSerialData
            {
                Serial = serial,
                CacheChecksum = cacheChecksum,
                Found = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read MTASA registry");
            return new MtasaSerialData { Found = false, ErrorMessage = ex.Message };
        }
    }
}
