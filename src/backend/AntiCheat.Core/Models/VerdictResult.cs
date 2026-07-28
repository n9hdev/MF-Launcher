namespace AntiCheat.Core.Models;

public class VerdictResult
{
    public double FinalConfidence { get; set; }
    public string Verdict { get; set; } = "unknown";
    public string SuggestedAction { get; set; } = "none";
    public string Explanation { get; set; } = string.Empty;
    public List<VerdictContribution> Contributions { get; set; } = new();
    public int TotalEvents { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public bool EscalationRequired { get; set; }
}

public class VerdictContribution
{
    public string DetectorType { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double ContributionScore { get; set; }
    public string TopEvent { get; set; } = string.Empty;
}
