using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("TeamCymruResults")]
public class TeamCymruResultEntity
{
    [Key]
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public int DetectionCount { get; set; }
    public int TotalEngines { get; set; }
    public double DetectionRate { get; set; }

    [MaxLength(64)]
    public string ScanResult { get; set; } = "unknown";

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}
