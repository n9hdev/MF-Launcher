using System.ComponentModel.DataAnnotations;

namespace AntiCheat.Core.Data.Entities;

public class AppealEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Player { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? BanId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string BanType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Date { get; set; } = string.Empty;
    public string Reviewer { get; set; } = "\u2014";

    public ICollection<AppealMessageEntity> Messages { get; set; } = new List<AppealMessageEntity>();
}
