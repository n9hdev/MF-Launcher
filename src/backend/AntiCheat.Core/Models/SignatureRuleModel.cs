using System.Text.Json.Serialization;

namespace AntiCheat.Core.Models;

public class SignatureRuleModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("matchType")]
    public string MatchType { get; set; } = string.Empty;

    [JsonPropertyName("conditions")]
    public RuleConditions? Conditions { get; set; }

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class RuleConditions
{
    [JsonPropertyName("minApiCount")]
    public int MinApiCount { get; set; } = 3;

    [JsonPropertyName("apis")]
    public List<string>? Apis { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("entropyThreshold")]
    public double EntropyThreshold { get; set; } = 7.0;

    [JsonPropertyName("codeEntropyThreshold")]
    public double CodeEntropyThreshold { get; set; } = 1.0;

    [JsonPropertyName("luaDlls")]
    public List<string>? LuaDlls { get; set; }

    [JsonPropertyName("gameFilePrefixes")]
    public List<string>? GameFilePrefixes { get; set; }

    [JsonPropertyName("suspiciousSectionNames")]
    public List<string>? SuspiciousSectionNames { get; set; }
}
