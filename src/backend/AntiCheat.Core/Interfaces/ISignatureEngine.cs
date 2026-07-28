using AntiCheat.Core.Models;

namespace AntiCheat.Core.Interfaces;

public interface ISignatureEngine
{
    IReadOnlyList<SignatureMatch> MatchPe(PeAnalysisResult peResult);
    IReadOnlyList<SignatureMatch> MatchProcessName(string processName);
    IReadOnlyList<SignatureMatch> MatchFilePath(string filePath);
    void ReloadRules(IEnumerable<SignatureRuleModel> rules);
}

public class SignatureMatch
{
    public string RuleName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium";
    public string MatchType { get; init; } = "pe"; // pe, process, filepath
    public string? MatchedValue { get; init; }
    public List<string> Tags { get; init; } = new();
}
