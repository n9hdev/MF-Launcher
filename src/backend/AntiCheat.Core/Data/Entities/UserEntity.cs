using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class UserEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "player";
    public int TrustScore { get; set; }
    public int Level { get; set; }
    public string Status { get; set; } = "offline";
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public int Xp { get; set; }
    public int NextLevelXp { get; set; }
    public string? HardwareId { get; set; }
    public string? SerialNumber { get; set; }
    public string? GamePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
