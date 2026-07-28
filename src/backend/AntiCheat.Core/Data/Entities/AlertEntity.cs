using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class AlertEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public int Confidence { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool Resolved { get; set; }
}
