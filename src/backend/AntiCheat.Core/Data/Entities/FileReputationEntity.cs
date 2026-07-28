using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("FileReputation")]
public class FileReputationEntity
{
    [Key]
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Md5 { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(256)]
    public string? ProductName { get; set; }

    [MaxLength(64)]
    public string? FileVersion { get; set; }

    [MaxLength(512)]
    public string? Signer { get; set; }

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public int TimesSeen { get; set; } = 1;

    public int TimesFlagged { get; set; }

    public int UniquePlayers { get; set; } = 1;

    [MaxLength(32)]
    public string Verdict { get; set; } = "unknown";

    public DateTime? LastAnalysisTime { get; set; }

    [MaxLength(1024)]
    public string? AnalysisNotes { get; set; }

    public double ConfidenceScore { get; set; }

    public bool IsLocalOverride { get; set; }
}
