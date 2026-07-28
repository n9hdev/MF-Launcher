using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class BanEntryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Player { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "Temporary";
    public string IssuedBy { get; set; } = string.Empty;
    public string IssuedAt { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? IpAddress { get; set; }
    public string? ProofUrl { get; set; }
    public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    public double DurationHours { get; set; } = -1;
    public bool Active { get; set; }
    public int Appeals { get; set; }
}
