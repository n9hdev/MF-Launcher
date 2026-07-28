namespace AntiCheat.Core.Interfaces;

public interface IMemoryScannerService
{
    Task<IReadOnlyList<MemoryEvidence>> ScanAsync(CancellationToken ct = default);
}

public class MemoryEvidence
{
    public string EvidenceType { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium";
    public string Description { get; init; } = string.Empty;
    public string? ProcessName { get; init; }
    public int ProcessId { get; init; }
    public string? Detail { get; init; }
    public long Address { get; init; }
    public long RegionSize { get; init; }
    public List<string> Tags { get; init; } = new();
}
