using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class RefreshTokenEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
