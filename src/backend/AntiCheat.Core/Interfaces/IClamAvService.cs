namespace AntiCheat.Core.Interfaces;

public class ClamAvResult
{
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsInfected { get; set; }
    public string VirusName { get; set; } = string.Empty;
    public string ScanResult { get; set; } = "unknown";
    public double ScanDurationMs { get; set; }
    public string Severity { get; set; } = "info";
    public string Description { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "ClamAvScan";
}

public interface IClamAvService
{
    bool IsAvailable { get; }
    Task<ClamAvResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClamAvResult>> ScanGameDirectoryAsync(CancellationToken cancellationToken = default);
}
