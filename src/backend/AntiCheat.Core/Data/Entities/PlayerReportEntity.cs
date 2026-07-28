using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

public class PlayerReportEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TicketType { get; set; } = "report_player";
    public string PlayerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
    public string? ReporterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool ChatEnabled { get; set; }
    public bool IsFlagged { get; set; }
    public string? AttachmentUrl { get; set; }

    [ForeignKey(nameof(ReporterId))]
    public UserEntity? Reporter { get; set; }

    public ICollection<ReportMessageEntity> Messages { get; set; } = new List<ReportMessageEntity>();
}
