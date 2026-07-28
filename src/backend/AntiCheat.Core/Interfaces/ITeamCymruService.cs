namespace AntiCheat.Core.Interfaces;

public class TeamCymruResult
{
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int DetectionCount { get; set; }
    public int TotalEngines { get; set; }
    public double DetectionRate { get; set; }
    public string ScanResult { get; set; } = "unknown";
    public string Severity { get; set; } = "info";
    public string Description { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "TeamCymruMhr";
}

public interface ITeamCymruService
{
    bool IsAvailable { get; }
    Task<TeamCymruResult> LookupHashAsync(string sha256, string? filePath = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamCymruResult>> ScanGameDirectoryAsync(CancellationToken cancellationToken = default);
}
