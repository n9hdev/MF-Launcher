using AntiCheat.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class MtaBaselineProvider : IMtaBaselineProvider
{
    private readonly ILogger<MtaBaselineProvider> _logger;
    private readonly string? _mtaInstallDir;

    private static readonly HashSet<string> KnownMtaModuleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mtasa.dll", "game_sa.dll", "multiplayer_sa.dll",
        "cgui.dll", "netc.dll",
        "ogg.dll", "vorbis.dll", "vorbisfile.dll", "eax.dll",
        "libcef.dll", "d3dx9_42.dll",
        "cef.pak", "cef_100_percent.pak", "cef_200_percent.pak",
        "cef_extensions.pak", "devtools_resources.pak",
        "icudtl.dat", "libcef.so", "libEGL.dll", "libGLESv2.dll",
        "snapshot_blob.bin", "v8_context_snapshot.bin",
        "chrome_elf.dll", "widevinecdm.dll",
        "gta_sa.exe", "gta_sa_compact.exe",
        "bass.dll", "bass_fx.dll", "basswma.dll",
        "d3dcompiler_43.dll", "d3dcompiler_47.dll",
        "d3dx9_43.dll",
        "msvcp100.dll", "msvcr100.dll",
        "msvcp120.dll", "msvcr120.dll",
        "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll",
        "conhost.exe",
    };

    private static readonly HashSet<string> KnownOverlayModuleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord.dll", "discord_game_sdk.dll",
        "steam_api.dll", "steam_api64.dll",
        "steamclient.dll", "steamclient64.dll",
        "nvspcaps.dll", "nvwgf2um.dll", "nvwgf2umx.dll",
        "dxgi.dll", "d3d9.dll", "d3d11.dll", "d3d12.dll",
        "winhttp.dll", "version.dll",
        "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "reshade.dll", "reShade64.dll",
        "obs.dll", "obs64.dll", "graphics-hook.dll", "graphics-hook64.dll",
        "rtss.dll", "rtssHooks.dll", "rtssHooks64.dll",
        "afterburner.dll",
        "fraps.dll",
        "gfe_overlay.dll", "galaxy.dll", "galaxy64.dll",
        "epic_online.dll",
        "socialclub.dll",
        "nvdaum.dll",
        "amdxx64.dll", "amdxc64.dll",
        "dxcompiler.dll",
        "opengl32.dll",
    };

    public string? MtaInstallDirectory => _mtaInstallDir;

    public IReadOnlySet<string> KnownMtaModules => KnownMtaModuleNames;
    public IReadOnlySet<string> KnownOverlayModules => KnownOverlayModuleNames;

    private static readonly HashSet<string> MtaProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "gta_sa", "gta_sa.exe",
        "mtasa", "mtasa.exe",
        "multiplayer_sa", "multiplayer_sa.exe",
    };

    public MtaBaselineProvider(ILogger<MtaBaselineProvider> logger, IMtasaPathFinder pathFinder)
    {
        _logger = logger;
        try
        {
            _mtaInstallDir = pathFinder.FindPath();
            if (!string.IsNullOrEmpty(_mtaInstallDir))
            {
                _logger.LogInformation("MTA install directory resolved: {Path}", _mtaInstallDir);
            }
            else
            {
                _logger.LogWarning("Could not resolve MTA install directory; using known module names only");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve MTA install directory; using known module names only");
            _mtaInstallDir = null;
        }
    }

    public bool IsMtaProcess(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName) &&
               MtaProcessNames.Contains(processName.Trim());
    }

    public bool IsKnownMtaModule(string moduleName)
    {
        return !string.IsNullOrWhiteSpace(moduleName) &&
               KnownMtaModuleNames.Contains(moduleName.Trim());
    }

    public bool IsKnownMtaModulePath(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath)) return false;

        var fileName = Path.GetFileName(modulePath);
        if (KnownMtaModuleNames.Contains(fileName)) return true;

        return IsUnderMtaInstallDir(modulePath);
    }

    public bool IsUnderMtaInstallDir(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(_mtaInstallDir))
            return false;

        return filePath.StartsWith(_mtaInstallDir, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsKnownOverlayOrLegitimateModule(string moduleName)
    {
        return !string.IsNullOrWhiteSpace(moduleName) &&
               KnownOverlayModuleNames.Contains(moduleName.Trim());
    }
}
