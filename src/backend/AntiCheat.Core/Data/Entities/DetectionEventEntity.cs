using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

public class DetectionEventEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? EvidencePath { get; set; }
    public string? PlayerId { get; set; }
    public string? ProcessName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PlayerId))]
    public UserEntity? Player { get; set; }
}
