using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IDetector
{
    string Name { get; }
    string Version { get; }
    bool IsEnabled { get; set; }
    Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default);
    Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default);
}

public class DetectionResult
{
    public bool ThreatDetected { get; set; }
    public double Confidence { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> Evidence { get; set; } = new();
}

public class DetectionContext
{
    public string? PlayerId { get; set; }
    public int ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
