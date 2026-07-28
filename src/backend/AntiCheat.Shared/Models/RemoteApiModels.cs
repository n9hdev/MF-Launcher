using System.Text.Json.Serialization;

namespace AntiCheat.Shared.Models;

public class HeartbeatRequest
{
    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "online";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("game_running")]
    public bool GameRunning { get; set; }
}

public class HeartbeatResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class GlobalVerifyRequest
{
    [JsonPropertyName("serial")]
    public string Serial { get; set; } = string.Empty;

    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonPropertyName("hardware")]
    public HardwareFingerprint Hardware { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HardwareFingerprint
{
    [JsonPropertyName("cpu_id")]
    public string CpuId { get; set; } = "no-cpu";

    [JsonPropertyName("gpu_id")]
    public string GpuId { get; set; } = "no-gpu";

    [JsonPropertyName("disk_id")]
    public string DiskId { get; set; } = "no-disk";

    [JsonPropertyName("os_version")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("machine_name")]
    public string MachineName { get; set; } = string.Empty;
}

public class GlobalVerifyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("ban_reason")]
    public string? BanReason { get; set; }
}

public class AntiReportRequest
{
    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("trusted")]
    public bool Trusted { get; set; }

    [JsonPropertyName("version")]
    public double Version { get; set; }

    [JsonPropertyName("threats")]
    public List<ReportedThreat> Threats { get; set; } = new();

    [JsonPropertyName("avStatus")]
    public string AvStatus { get; set; } = "OK";

    [JsonPropertyName("screenshots")]
    public List<ReportedScreenshot>? Screenshots { get; set; }

    [JsonPropertyName("systemInfo")]
    public ReportedSystemInfo SystemInfo { get; set; } = new();
}

public class ReportedThreat
{
    [JsonPropertyName("process")]
    public string Process { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "low";

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = new();

    [JsonPropertyName("gameRelated")]
    public bool GameRelated { get; set; }
}

public class ReportedScreenshot
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

public class ReportedSystemInfo
{
    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; set; }

    [JsonPropertyName("is64Bit")]
    public bool Is64Bit { get; set; }
}

public class UpdateCheckResponse
{
    [JsonPropertyName("has_update")]
    public bool HasUpdate { get; set; }

    [JsonPropertyName("latest_version")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("release_notes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("critical")]
    public bool Critical { get; set; }
}

public class PlayerProfileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("player_id")]
    public int PlayerId { get; set; }

    [JsonPropertyName("hwid")]
    public string? Hwid { get; set; }

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("trusted")]
    public bool Trusted { get; set; }

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("statistics")]
    public ProfileStatistics? Statistics { get; set; }

    [JsonPropertyName("security")]
    public ProfileSecurity? Security { get; set; }

    [JsonPropertyName("activity")]
    public ProfileActivity? Activity { get; set; }

    [JsonPropertyName("session_info")]
    public ProfileSessionInfo? SessionInfo { get; set; }

    [JsonPropertyName("metadata")]
    public ProfileMetadata? Metadata { get; set; }
}

public class ProfileStatistics
{
    [JsonPropertyName("login_count")]
    public int LoginCount { get; set; }

    [JsonPropertyName("total_play_time_seconds")]
    public int TotalPlayTimeSeconds { get; set; }

    [JsonPropertyName("total_play_time_formatted")]
    public string? TotalPlayTimeFormatted { get; set; }

    [JsonPropertyName("detections_count")]
    public int DetectionsCount { get; set; }

    [JsonPropertyName("threat_level")]
    public string? ThreatLevel { get; set; }
}

public class ProfileSecurity
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }

    [JsonPropertyName("hardware_baseline_set")]
    public bool HardwareBaselineSet { get; set; }
}

public class ProfileActivity
{
    [JsonPropertyName("first_login")]
    public DateTime? FirstLogin { get; set; }

    [JsonPropertyName("last_login")]
    public DateTime? LastLogin { get; set; }

    [JsonPropertyName("last_logout")]
    public DateTime? LastLogout { get; set; }

    [JsonPropertyName("last_check")]
    public DateTime? LastCheck { get; set; }

    [JsonPropertyName("last_seen")]
    public DateTime? LastSeen { get; set; }

    [JsonPropertyName("current_ip")]
    public string? CurrentIp { get; set; }
}

public class ProfileSessionInfo
{
    [JsonPropertyName("total_sessions")]
    public int TotalSessions { get; set; }

    [JsonPropertyName("active_session")]
    public ActiveSession? ActiveSession { get; set; }
}

public class ActiveSession
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("login_time")]
    public DateTime? LoginTime { get; set; }

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("game_running")]
    public bool GameRunning { get; set; }
}

public class ProfileMetadata
{
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
