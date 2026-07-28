namespace AntiCheat.Shared.Models;

public class EvidenceFact
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string Category { get; init; } = string.Empty;
    public string Observation { get; init; } = string.Empty;
    public string Interpretation { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string SourceComponent { get; init; } = string.Empty;
    public bool IsAnomaly { get; init; }

    public List<string> Tags { get; init; } = new();
    public Dictionary<string, string> Details { get; init; } = new();
}
