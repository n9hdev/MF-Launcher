namespace AntiCheat.Core.Interfaces;

public interface IMtaBaselineProvider
{
    bool IsMtaProcess(string processName);
    bool IsKnownMtaModule(string moduleName);
    bool IsKnownMtaModulePath(string modulePath);
    bool IsUnderMtaInstallDir(string filePath);
    bool IsKnownOverlayOrLegitimateModule(string moduleName);
    IReadOnlySet<string> KnownMtaModules { get; }
    IReadOnlySet<string> KnownOverlayModules { get; }
    string? MtaInstallDirectory { get; }
}
