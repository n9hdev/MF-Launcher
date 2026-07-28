using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class DeviceEntity
{
    [Key]
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int TrustScore { get; set; }
    public bool IsVerified { get; set; }
    public string? UserId { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
