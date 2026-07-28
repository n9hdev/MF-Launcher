namespace AntiCheat.Core.Models;

public class BaselineSnapshot
{
    public DateTime CaptureTime { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;

    public ModuleRangeMap Modules { get; set; } = new();
    public List<MemoryRegionRecord> Regions { get; set; } = new();
    public List<ThreadRecord> Threads { get; set; } = new();
    public bool IsMtaInitialized { get; set; }
    public string MtaInstallPath { get; set; } = string.Empty;

    public DateTime ProcessStartTime { get; set; }
}

public class ModuleRangeMap
{
    public List<ModuleEntry> Entries { get; set; } = new();

    public string? ResolveModule(ulong address)
    {
        foreach (var entry in Entries)
        {
            if (address >= entry.BaseAddress && address < entry.BaseAddress + entry.SizeOfImage)
                return entry.ModuleName;
        }
        return null;
    }

    public string? ResolveModuleWithOffset(ulong address, out ulong offset, out string? fullPath)
    {
        offset = 0;
        fullPath = null;
        foreach (var entry in Entries)
        {
            if (address >= entry.BaseAddress && address < entry.BaseAddress + entry.SizeOfImage)
            {
                offset = address - entry.BaseAddress;
                fullPath = entry.FullPath;
                return entry.ModuleName;
            }
        }
        return null;
    }
}

public class ModuleEntry
{
    public string ModuleName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ulong BaseAddress { get; set; }
    public uint SizeOfImage { get; set; }
    public ulong CodeHash { get; set; }

    public bool PresentInPeb { get; set; }
    public bool PresentInToolhelp { get; set; }
    public bool PresentInPsapi { get; set; }

    public int LoadOrderIndex { get; set; }
}

public class MemoryRegionRecord
{
    public ulong BaseAddress { get; set; }
    public long RegionSize { get; set; }
    public uint Protect { get; set; }
    public uint Type { get; set; }
    public ulong? CodeHash { get; set; }
    public string? ModuleName { get; set; }
    public bool ContainsPeHeader { get; set; }
}

public class ThreadRecord
{
    public uint ThreadId { get; set; }
    public ulong StartAddress { get; set; }
    public string? StartModuleName { get; set; }
    public ulong StartModuleOffset { get; set; }
}
