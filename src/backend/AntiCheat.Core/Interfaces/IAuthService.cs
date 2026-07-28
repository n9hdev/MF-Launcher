using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    Task<UserDto> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ValidateTokenAsync(string token);
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(string userId, DeviceRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<List<SessionInfo>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default);
    Task TerminateSessionAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateProfileAsync(string userId, string? gamePath, string? hardwareId, string? serialNumber = null, CancellationToken cancellationToken = default);
    Task<HardwareVerificationResult> VerifyHardwareAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> GetTrustStatusAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}

public class HardwareVerificationResult
{
    public bool IsVerified { get; set; }
    public bool HwidStored { get; set; }
    public string? CurrentHwid { get; set; }
    public string? StoredHwid { get; set; }
    public bool Matches { get; set; }
}