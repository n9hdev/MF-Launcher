namespace AntiCheat.Shared.Models;

public class ModeratorStatsDto
{
    public int OpenReports { get; set; }
    public int ActivePlayers { get; set; }
    public int ActiveAlerts { get; set; }
    public int ResolvedToday { get; set; }
    public double AvgResponseTime { get; set; }
    public double ReportsPerHour { get; set; }
    public double BanAccuracy { get; set; }
}

public class ActiveAlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
}

public class AlertDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public int Confidence { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool Resolved { get; set; }
}

public class ModChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public string Role { get; set; } = "moderator";
    public DateTime CreatedAt { get; set; }
}

public class ModeratorOnlineDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
}

public class PlayerSearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int TrustScore { get; set; }
    public string Status { get; set; } = "offline";
    public string LastSeen { get; set; } = string.Empty;
    public string GameName { get; set; } = "MTA:SA";
    public int HoursPlayed { get; set; }
    public int ReportsCount { get; set; }
    public int BansCount { get; set; }
    public string? Avatar { get; set; }
}

public class SendMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

public class PlayerDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? HardwareId { get; set; }
    public string? SerialNumber { get; set; }
    public string? HardwareFingerprint { get; set; }
    public string Status { get; set; } = "clean";
    public string? GameName { get; set; }
    public int TrustScore { get; set; }
    public int HoursPlayed { get; set; }
    public int ReportsCount { get; set; }
    public int BansCount { get; set; }
    public string LastSeen { get; set; } = string.Empty;
    public List<SessionSummaryDto> Sessions { get; set; } = new();
    public List<DetectionEventDto> Detections { get; set; } = new();
}

public class SessionSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UpdateReportStatusRequest
{
    public string Status { get; set; } = string.Empty;
}


