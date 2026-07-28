namespace AntiCheat.Shared.Models;

public class DeviceRegistrationRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
}

public class DeviceRegistrationResponse
{
    public bool Success { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int TrustScore { get; set; }
    public bool RequiresVerification { get; set; }
}

public class DeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int TrustScore { get; set; }
    public bool IsVerified { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActivity { get; set; }
    public bool IsActive { get; set; }
}