namespace AntiCheat.Core.Models;

public class EngineRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Category { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public RuleConditions? Conditions { get; set; }
    public List<string> Patterns { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public int HitCount { get; set; }
    public DateTime? LastMatchTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
