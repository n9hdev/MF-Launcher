namespace AntiCheat.Core.Models;

public class PeAnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool ParsingSucceeded { get; set; }
    public string? ParseError { get; set; }

    public DosHeaderInfo DosHeader { get; set; } = new();
    public NtHeaderInfo NtHeader { get; set; } = new();
    public FileHeaderInfo FileHeader { get; set; } = new();
    public OptionalHeaderInfo OptionalHeader { get; set; } = new();

    public List<SectionInfo> Sections { get; set; } = new();
    public ImportInfo Imports { get; set; } = new();
    public ExportInfo? Exports { get; set; }
    public ResourceInfo Resources { get; set; } = new();
    public SignatureInfo? Signature { get; set; }
    public RichHeaderInfo? RichHeader { get; set; }
    public DebugInfo Debug { get; set; } = new();
    public ClrInfo Clr { get; set; } = new();
    public TlsInfo Tls { get; set; } = new();
    public OverlayInfo Overlay { get; set; } = new();
    public HashInfo Hashes { get; set; } = new();
    public PackerInfo Packer { get; set; } = new();
    public EntropyInfo Entropy { get; set; } = new();
}

public class DosHeaderInfo
{
    public ushort Magic { get; set; }
    public uint LfaNew { get; set; }
    public bool Valid => Magic == 0x5A4D;
}

public class NtHeaderInfo
{
    public uint Signature { get; set; }
    public uint Offset { get; set; }
    public bool Valid => Signature == 0x00004550;
}

public class FileHeaderInfo
{
    public ushort Machine { get; set; }
    public string MachineName => Machine switch
    {
        0x014C => "I386 (x86)",
        0x8664 => "AMD64 (x64)",
        0x0200 => "Intel Itanium",
        0x01C4 => "ARM NT",
        0xAA64 => "ARM64",
        _ => $"Unknown (0x{Machine:X4})",
    };
    public ushort NumberOfSections { get; set; }
    public uint TimeDateStamp { get; set; }
    public DateTime CompileTime => DateTimeOffset.FromUnixTimeSeconds(TimeDateStamp).UtcDateTime;
    public bool FutureCompileDate => CompileTime > DateTime.UtcNow.AddDays(1);
    public uint PointerToSymbolTable { get; set; }
    public uint NumberOfSymbols { get; set; }
    public ushort SizeOfOptionalHeader { get; set; }
    public ushort Characteristics { get; set; }
    public List<string> CharacteristicsFlags { get; set; } = new();
}

public class OptionalHeaderInfo
{
    public ushort Magic { get; set; }
    public bool IsPe32Plus => Magic == 0x020B;
    public string Architecture => Magic switch
    {
        0x010B => "PE32 (x86)",
        0x020B => "PE32+ (x64)",
        _ => $"Unknown (0x{Magic:X4})",
    };
    public byte MajorLinkerVersion { get; set; }
    public byte MinorLinkerVersion { get; set; }
    public string LinkerVersion => $"{MajorLinkerVersion}.{MinorLinkerVersion}";
    public uint SizeOfCode { get; set; }
    public uint SizeOfInitializedData { get; set; }
    public uint SizeOfUninitializedData { get; set; }
    public uint AddressOfEntryPoint { get; set; }
    public ulong BaseOfCode { get; set; }
    public ulong ImageBase { get; set; }
    public uint SectionAlignment { get; set; }
    public uint FileAlignment { get; set; }
    public ushort MajorOperatingSystemVersion { get; set; }
    public ushort MinorOperatingSystemVersion { get; set; }
    public ushort MajorImageVersion { get; set; }
    public ushort MinorImageVersion { get; set; }
    public ushort MajorSubsystemVersion { get; set; }
    public ushort MinorSubsystemVersion { get; set; }
    public uint SizeOfImage { get; set; }
    public uint SizeOfHeaders { get; set; }
    public uint Subsystem { get; set; }
    public string SubsystemName => Subsystem switch
    {
        0 => "Unknown",
        1 => "Native",
        2 => "Windows GUI",
        3 => "Windows Console",
        5 => "OS2 Console",
        7 => "POSIX Console",
        9 => "Windows CE GUI",
        10 => "EFI Application",
        11 => "EFI Boot Service Driver",
        12 => "EFI Runtime Driver",
        13 => "EFI ROM",
        14 => "XBOX",
        16 => "Windows Boot Application",
        _ => $"Unknown ({Subsystem})",
    };
    public ushort DllCharacteristics { get; set; }
    public List<string> DllCharacteristicsFlags { get; set; } = new();
    public ulong SizeOfStackReserve { get; set; }
    public ulong SizeOfStackCommit { get; set; }
    public ulong SizeOfHeapReserve { get; set; }
    public ulong SizeOfHeapCommit { get; set; }
    public uint NumberOfRvaAndSizes { get; set; }
}

public class SectionInfo
{
    public string Name { get; set; } = string.Empty;
    public uint VirtualSize { get; set; }
    public uint VirtualAddress { get; set; }
    public uint RawSize { get; set; }
    public uint RawOffset { get; set; }
    public uint Characteristics { get; set; }
    public double Entropy { get; set; }
    public bool IsExecutable => (Characteristics & 0x20000000) != 0;
    public bool IsWritable => (Characteristics & 0x80000000) != 0;
    public bool IsDiscardable => (Characteristics & 0x02000000) != 0;
    public bool IsCode => (Characteristics & 0x00000020) != 0;
    public bool IsInitializedData => (Characteristics & 0x00000040) != 0;

    public bool SuspiciousName => SuspiciousSectionNames.Any(s =>
        Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
    public bool EmptySection => RawSize == 0 && VirtualSize == 0;
    public bool OverlappingSection => false;

    private static readonly string[] SuspiciousSectionNames =
        { "cheat", "hack", "crack", "loader", "inject", "dll", "evil", "bad" };
}

public class ImportInfo
{
    public List<string> Dlls { get; set; } = new();
    public Dictionary<string, List<string>> Imports { get; set; } = new();
    public List<DangerousImport> DangerousImports { get; set; } = new();
    public bool HasDelayedImports { get; set; }
    public List<string> DelayedImportDlls { get; set; } = new();
}

public class DangerousImport
{
    public string ApiName { get; set; } = string.Empty;
    public string DllName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ExportInfo
{
    public uint ExportFlags { get; set; }
    public uint Timestamp { get; set; }
    public ushort MajorVersion { get; set; }
    public ushort MinorVersion { get; set; }
    public string? Name { get; set; }
    public uint NameRva { get; set; }
    public uint Base { get; set; }
    public uint NumberOfFunctions { get; set; }
    public uint NumberOfNames { get; set; }
    public List<string> FunctionNames { get; set; } = new();
    public List<string> ForwardedFunctions { get; set; } = new();
}

public class ResourceInfo
{
    public bool HasResources { get; set; }
    public List<ResourceEntry> Entries { get; set; } = new();
    public VersionInfo? Version { get; set; }
    public string? Manifest { get; set; }
}

public class ResourceEntry
{
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Language { get; set; }
    public uint Size { get; set; }
    public uint Offset { get; set; }
}

public class VersionInfo
{
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? ProductVersion { get; set; }
    public string? FileVersion { get; set; }
    public string? OriginalFilename { get; set; }
    public string? FileDescription { get; set; }
    public string? LegalCopyright { get; set; }
    public string? InternalName { get; set; }
    public string? PrivateBuild { get; set; }
    public string? SpecialBuild { get; set; }
}

public class SignatureInfo
{
    public bool IsSigned { get; set; }
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public string? Thumbprint { get; set; }
    public DateTime? NotBefore { get; set; }
    public DateTime? NotAfter { get; set; }
    public bool IsSelfSigned { get; set; }
    public bool IsRevoked { get; set; }
    public string? ChainStatus { get; set; }
}

public class RichHeaderInfo
{
    public bool Present { get; set; }
    public uint Offset { get; set; }
    public uint Length { get; set; }
    public List<RichHeaderEntry> Entries { get; set; } = new();
    public string? Hash { get; set; }
}

public class RichHeaderEntry
{
    public ushort ProductId { get; set; }
    public ushort BuildId { get; set; }
    public uint Count { get; set; }
    public string? ProductName { get; set; }
    public string? ToolDescription { get; set; }
}

public class DebugInfo
{
    public bool HasDebugDirectory { get; set; }
    public string? PdbPath { get; set; }
    public string? Guid { get; set; }
    public uint? Age { get; set; }
    public string? DebugType { get; set; }
    public bool StrippedSymbols => !HasDebugDirectory;
}

public class ClrInfo
{
    public ClrType Type { get; set; } = ClrType.Native;
    public ushort ClrMajor { get; set; }
    public ushort ClrMinor { get; set; }
    public uint ClrFlags { get; set; }
    public ulong ClrMetaDataRva { get; set; }
    public ulong ClrMetaDataSize { get; set; }
}

public enum ClrType
{
    Native,
    NetAssembly,
    NetMixedMode,
}

public class TlsInfo
{
    public bool HasTls { get; set; }
    public List<ulong> CallbackAddresses { get; set; } = new();
    public int CallbackCount => CallbackAddresses.Count;
}

public class OverlayInfo
{
    public bool Exists { get; set; }
    public long Offset { get; set; }
    public long Size { get; set; }
    public double Entropy { get; set; }
}

public class HashInfo
{
    public string? Sha256 { get; set; }
    public string? Sha1 { get; set; }
    public string? Md5 { get; set; }
    public string? ImpHash { get; set; }
}

public class PackerInfo
{
    public bool IsPacked { get; set; }
    public string? PackerName { get; set; }
    public double Confidence { get; set; }
    public List<string> DetectedSignatures { get; set; } = new();
}

public class EntropyInfo
{
    public double FileEntropy { get; set; }
    public double OverlayEntropy { get; set; }
    public double? MaxSectionEntropy { get; set; }
    public double? MinSectionEntropy { get; set; }
    public string? HighestEntropySection { get; set; }
    public bool HighFileEntropy => FileEntropy > 7.0;
    public bool HighOverlayEntropy => OverlayEntropy > 7.0;
}
