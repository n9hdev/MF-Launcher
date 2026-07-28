using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("DetectionFingerprints")]
public class DetectionFingerprintEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(64)]
    public string Fingerprint { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? PlayerId { get; set; }

    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public int HitCount { get; set; } = 1;
}
