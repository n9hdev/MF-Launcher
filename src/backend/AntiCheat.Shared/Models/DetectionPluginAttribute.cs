namespace AntiCheat.Shared.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DetectionPluginAttribute : Attribute
{
    public string Name { get; }
    public string Version { get; }
    public string Description { get; }

    public DetectionPluginAttribute(string name, string version, string description)
    {
        Name = name;
        Version = version;
        Description = description;
    }
}

public class RiskAssessmentDto
{
    public double OverallRiskScore { get; set; }
    public string RiskLevel { get; set; } = "low";
    public List<DetectionEventDto> ActiveThreats { get; set; } = new();
    public Dictionary<string, double> DetectorContributions { get; set; } = new();
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

public class EvidenceRecordDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DetectionEventId { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public string? FilePath { get; set; }
}

public class CorrelationResultDto
{
    public double CorrelationScore { get; set; }
    public string CorrelationLevel { get; set; } = "normal";
    public List<CorrelationSignalDto> Signals { get; set; } = new();
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public bool AutoEscalate { get; set; }
    public double AdjustedScanInterval { get; set; } = 30;
}

public class CorrelationSignalDto
{
    public string DetectorName { get; set; } = string.Empty;
    public string SignalType { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Confidence { get; set; }
    public double Contribution { get; set; }
}

public class AntiTamperStatusDto
{
    public bool DebuggerDetected { get; set; }
    public bool IntegrityCheckPassed { get; set; }
    public int VerifiedModules { get; set; }
    public int FailedModules { get; set; }
    public List<string> Alerts { get; set; } = new();
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

public class ScreenshotCaptureDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string? DetectionEventId { get; set; }
    public string? SessionId { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string Format { get; set; } = "jpeg";
    public double RiskScore { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string? CapturedBy { get; set; }
    public string? HmacSignature { get; set; }
    public string? StoragePath { get; set; }
}

public class ScreenshotRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string? DetectionEventId { get; set; }
    public string? Reason { get; set; }
}

public class ScreenStreamSessionDto
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string Status { get; set; } = "requested";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int TotalFrames { get; set; }
    public double DurationSeconds { get; set; }
    public string? LinkedDetectionId { get; set; }
    public List<StreamViewerDto> Viewers { get; set; } = new();
    public double TargetFps { get; set; } = 2;
    public int JpegQuality { get; set; } = 60;
}

public class StreamViewerDto
{
    public string AdminId { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
}

public class ScreenFrameDto
{
    public string SessionId { get; set; } = string.Empty;
    public int FrameNumber { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "jpeg";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ScreenStreamConfigDto
{
    public double NormalFps { get; set; } = 2;
    public double SuspiciousFps { get; set; } = 7;
    public double AdminRequestedFps { get; set; } = 15;
    public int JpegQuality { get; set; } = 60;
    public int MinJpegQuality { get; set; } = 50;
    public int MaxViewers { get; set; } = 3;
    public int StreamTimeoutSeconds { get; set; } = 300;
    public int FrameRateLimitPerSecond { get; set; } = 30;
}

public class StreamSummaryDto
{
    public string SessionId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public double DurationSeconds { get; set; }
    public int TotalFrames { get; set; }
    public int ViewerCount { get; set; }
    public string? LinkedDetectionId { get; set; }
}

public class DetectionRuleDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DetectorType { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public double ConfidenceThreshold { get; set; } = 0.5;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
