namespace AntiCheat.Shared.Models;

public class AdminStatsDto
{
    public int ActiveBans { get; set; }
    public int PendingAppeals { get; set; }
    public double DetectionRate { get; set; }
    public int TotalPlayers { get; set; }
    public int PermanentBans { get; set; }
    public int TemporaryBans { get; set; }
}

public class DetectorPerformanceDto
{
    public string Name { get; set; } = string.Empty;
    public int Detections { get; set; }
    public double Accuracy { get; set; }
    public string Status { get; set; } = "active";
}

public class AdminBanEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Player { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "Temporary";
    public string IssuedBy { get; set; } = string.Empty;
    public string IssuedAt { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int Appeals { get; set; }
    public string? SerialNumber { get; set; }
    public string? IpAddress { get; set; }
    public string? ProofUrl { get; set; }
    public DateTime BannedAt { get; set; }
    public double DurationHours { get; set; } = -1;
}

public class WeeklyActivityDto
{
    public string Day { get; set; } = string.Empty;
    public int Scans { get; set; }
    public int Threats { get; set; }
    public int Players { get; set; }
}

public class ThreatDistributionDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Pct { get; set; }
}

public class TopReportDto
{
    public string Player { get; set; } = string.Empty;
    public int Reports { get; set; }
    public string Action { get; set; } = string.Empty;
}

public class AdminAppealDto
{
    public string Id { get; set; } = string.Empty;
    public string Player { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? BanId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string BanType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Date { get; set; } = string.Empty;
    public string Reviewer { get; set; } = "—";
    public List<AppealMessageDto> Messages { get; set; } = new();
}

public class AppealMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string AppealId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SendAppealMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

public class WhitelistEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Entry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public string AddedAt { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class CreateBanRequest
{
    public string Player { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "Temporary";
    public string IssuedBy { get; set; } = string.Empty;
}

public class UpdateBanRequest
{
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = "Temporary";
    public bool Active { get; set; } = true;
}

public class AddWhitelistEntryRequest
{
    public string Entry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class UpdateWhitelistEntryRequest
{
    public string Entry { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class UpdateAppealStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string Reviewer { get; set; } = string.Empty;
}
