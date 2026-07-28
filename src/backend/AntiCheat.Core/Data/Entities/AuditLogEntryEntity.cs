using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class AuditLogEntryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
}
