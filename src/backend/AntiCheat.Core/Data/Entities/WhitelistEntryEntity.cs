using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class WhitelistEntryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Entry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public string AddedAt { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
