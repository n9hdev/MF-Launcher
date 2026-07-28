namespace AntiCheat.Shared.Models;

public class ServiceHeartbeatRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? GamePath { get; set; }
    public string Status { get; set; } = "idle";
    public bool GameRunning { get; set; }
    public int DetectionCount { get; set; }
    public string? Version { get; set; }
    public ProtectionStatusDto? ProtectionStatus { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
}

public class ServiceHeartbeatResponse
{
    public List<string> Commands { get; set; } = new();
    public Dictionary<string, string> CommandData { get; set; } = new();
}

public class ServiceScreenshotUploadRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? DetectionEventId { get; set; }
    public string? Reason { get; set; }
    public string? RequestId { get; set; }
    public string ImageBase64 { get; set; } = string.Empty;
    public string Format { get; set; } = "jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceScreenshotUploadResponse
{
    public bool Success { get; set; }
    public string? CloudUrl { get; set; }
    public string? ScreenshotId { get; set; }
    public string? Error { get; set; }
}

public class ServiceStreamFrameRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public string Format { get; set; } = "jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ServiceStreamFrameResponse
{
    public bool Success { get; set; }
    public int FrameNumber { get; set; }
    public double TargetFps { get; set; }
    public bool ShouldStop { get; set; }
}

public class ServiceDetectionReport
{
    public string HardwareId { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string ScanType { get; set; } = "continuous";
    public List<DetectionEventDto> Detections { get; set; } = new();
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceDetectionResponse
{
    public bool Received { get; set; }
    public int Count { get; set; }
    public bool Banned { get; set; }
}

public class GameHashVerifyRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public List<GameFileHashEntry> Files { get; set; } = new();
}

public class GameFileHashEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Md5 { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class GameHashVerifyResponse
{
    public List<GameFileHashResult> Results { get; set; } = new();
}

public class GameFileHashResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = "new"; // match | mismatch | new
    public string? ExpectedSha256 { get; set; }
    public long? ExpectedFileSize { get; set; }
}
