using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class SessionEntity
{
    [Key]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivity { get; set; }
    public bool IsActive { get; set; } = true;
}
