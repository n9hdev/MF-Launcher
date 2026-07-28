using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class ActivityEventEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string? Icon { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
