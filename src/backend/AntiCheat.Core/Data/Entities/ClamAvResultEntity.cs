using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("ClamAvResults")]
public class ClamAvResultEntity
{
    [Key]
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public bool IsInfected { get; set; }

    [MaxLength(256)]
    public string VirusName { get; set; } = string.Empty;

    [MaxLength(32)]
    public string ScanResult { get; set; } = "unknown";

    public long FileSize { get; set; }
    public double ScanDurationMs { get; set; }

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMatch { get; set; } = DateTime.UtcNow;
}
