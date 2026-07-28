namespace AntiCheat.Shared.Models;

public class ActivityEventDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Severity { get; set; }
    public string? Icon { get; set; }
}

public class TimelineEventDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Severity { get; set; } = "info";
    public string? Category { get; set; }
    public double? Confidence { get; set; }
    public int Count { get; set; } = 1;
}

public class HistorySummaryDto
{
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    public int Info { get; set; }
}

public class DetectionStatsDto
{
    public int TotalScans { get; set; }
    public int ThreatsFound { get; set; }
    public int FalsePositives { get; set; }
    public double UptimePercent { get; set; }
    public int CleanScans { get; set; }
}

public class PlayerReportDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TicketType { get; set; } = "report_player";
    public string PlayerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Result { get; set; }
    public string? ReporterId { get; set; }
    public bool ChatEnabled { get; set; }
    public bool IsFlagged { get; set; }
    public string? AttachmentUrl { get; set; }
    public List<ReportMessageDto> Messages { get; set; } = new();
}

public class ReportMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendReportMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ToggleChatRequest
{
    public bool ChatEnabled { get; set; }
}

public class FlagReportRequest
{
    public bool IsFlagged { get; set; }
}

public class ReportSubmissionRequest
{
    public string TicketType { get; set; } = "report_player";
    public string PlayerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class LaunchCheckDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "passed";
    public string? Details { get; set; }
}

public class GameSettingsDto
{
    public bool WindowedMode { get; set; }
    public bool SkipIntro { get; set; }
    public bool DevConsole { get; set; }
}

public class GameStatusDto
{
    public bool IsRunning { get; set; }
    public string? ProcessName { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan? Uptime { get; set; }
}

public class ModuleControlRequest
{
    public string ModuleName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
