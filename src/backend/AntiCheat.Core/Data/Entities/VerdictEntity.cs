using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("VerdictHistory")]
public class VerdictEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(64)]
    public string? PlayerId { get; set; }

    public double FinalConfidence { get; set; }

    [MaxLength(32)]
    public string Verdict { get; set; } = "unknown";

    [MaxLength(32)]
    public string SuggestedAction { get; set; } = "none";

    [MaxLength(2000)]
    public string Explanation { get; set; } = string.Empty;

    public int TotalEvents { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }

    [MaxLength(2000)]
    public string ContributionsJson { get; set; } = "[]";

    public bool EscalationRequired { get; set; }

    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}
