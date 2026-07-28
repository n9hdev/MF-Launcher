namespace AntiCheat.Shared.Models;

public class ReputationLookupRequest
{
    public string Sha256 { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? ProcessName { get; set; }
}

public class ReputationLookupResponse
{
    public string Sha256 { get; set; } = string.Empty;
    public string Verdict { get; set; } = "unknown";
    public double ConfidenceScore { get; set; }
    public bool IsOverride { get; set; }
    public string? AnalysisNotes { get; set; }
}

public class ReputationReportRequest
{
    public string Sha256 { get; set; } = string.Empty;
    public string? Md5 { get; set; }
    public long? FileSize { get; set; }
    public string? ProductName { get; set; }
    public string? FileVersion { get; set; }
    public string? Signer { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? ProcessName { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public double LocalConfidence { get; set; }
}

public class ReputationVerdictRequest
{
    public string Sha256 { get; set; } = string.Empty;
    public string Verdict { get; set; } = "unknown";
    public string? Notes { get; set; }
}
