using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class StreamSessionEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public double TargetFps { get; set; } = 1;
    public int JpegQuality { get; set; } = 50;
    public int TotalFrames { get; set; }
    public string? EndedReason { get; set; }
    public string? StartedBy { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
