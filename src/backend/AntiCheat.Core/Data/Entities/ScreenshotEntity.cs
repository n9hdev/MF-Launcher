using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

public class ScreenshotEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string? DetectionEventId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CloudinaryUrl { get; set; } = string.Empty;
    public string? CloudinaryPublicId { get; set; }
    public long FileSize { get; set; }
    public string? HmacSignature { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = "captured";
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string? CapturedBy { get; set; }
}
