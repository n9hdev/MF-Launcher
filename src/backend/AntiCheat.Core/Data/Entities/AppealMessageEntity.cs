using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class AppealMessageEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AppealId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
