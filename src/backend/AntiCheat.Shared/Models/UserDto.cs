namespace AntiCheat.Shared.Models;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "player";
    public int TrustScore { get; set; }
    public string TrustStatus { get; set; } = "pending";
    public int Level { get; set; }
    public string Status { get; set; } = "offline";
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public int Xp { get; set; }
    public int NextLevelXp { get; set; }
    public string? HardwareId { get; set; }
    public string? SerialNumber { get; set; }
    public string? GamePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
}

public class LoginResponse
{
    public UserDto User { get; set; } = null!;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public bool HwidVerified { get; set; }
    public bool IsBanned { get; set; }
    public string TrustStatus { get; set; } = "pending";
    public BanInfoDto? BanInfo { get; set; }
}

public class BanInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public string IssuedAt { get; set; } = string.Empty;
    public string? ProofUrl { get; set; }
    public double DurationHours { get; set; }
    public DateTime BannedAt { get; set; }
}

public class AuthTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? HardwareId { get; set; }
}