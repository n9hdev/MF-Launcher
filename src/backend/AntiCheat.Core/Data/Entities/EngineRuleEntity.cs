using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("EngineRules")]
public class EngineRuleEntity
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Severity { get; set; } = "medium";

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MatchType { get; set; } = string.Empty;

    [Column(TypeName = "longtext")]
    public string? ConditionsJson { get; set; }

    [Column(TypeName = "longtext")]
    public string PatternsJson { get; set; } = "[]";

    [Column(TypeName = "longtext")]
    public string TagsJson { get; set; } = "[]";

    public bool Enabled { get; set; } = true;

    public int HitCount { get; set; }

    public DateTime? LastMatchTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
