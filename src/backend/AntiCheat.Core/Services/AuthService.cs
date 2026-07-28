using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AntiCheat.Core.Services;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly JwtSettings _jwt;
    private readonly AppDbContext _db;
    private readonly IHardwareIdProvider _hardwareIdProvider;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TokenValidationParameters _validationParams;

    public AuthService(
        ILogger<AuthService> logger,
        IOptions<JwtSettings> jwt,
        AppDbContext db,
        IHardwareIdProvider hardwareIdProvider)
    {
        _logger = logger;
        _jwt = jwt.Value;
        _db = db;
        _hardwareIdProvider = hardwareIdProvider;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        _validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: invalid credentials for {Username}", request.Username);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // HWID cross-validation now happens via Service heartbeat — login does not block on HWID
        var hwidVerified = !string.IsNullOrEmpty(user.HardwareId);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, cancellationToken);

        var session = new SessionEntity
        {
            UserId = user.Id,
            DeviceId = request.DeviceId ?? "unknown",
            IpAddress = request.IpAddress ?? "unknown",
        };
        _db.Sessions.Add(session);

        if (user.Status == "banned")
        {
            var activeBan = await _db.BanEntries
                .Where(b => b.PlayerId == user.Id && b.Active)
                .OrderByDescending(b => b.BannedAt)
                .FirstOrDefaultAsync(cancellationToken);

            user.LastLoginAt = DateTime.UtcNow;
            CollectAndSaveHardwareInfo(user);
            await _db.SaveChangesAsync(cancellationToken);

            var bannedDto = MapToDto(user);
            bannedDto.TrustStatus = "restricted";

            return new LoginResponse
            {
                User = bannedDto,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                SessionId = session.SessionId,
                HwidVerified = hwidVerified,
                IsBanned = true,
                TrustStatus = "restricted",
                BanInfo = activeBan != null ? new BanInfoDto
                {
                    Id = activeBan.Id,
                    Reason = activeBan.Reason,
                    Type = activeBan.Type,
                    IssuedBy = activeBan.IssuedBy,
                    IssuedAt = activeBan.IssuedAt,
                    ProofUrl = activeBan.ProofUrl,
                    DurationHours = activeBan.DurationHours,
                    BannedAt = activeBan.BannedAt,
                } : null,
            };
        }

        user.Status = "online";
        user.LastLoginAt = DateTime.UtcNow;

        CollectAndSaveHardwareInfo(user);

        await _db.SaveChangesAsync(cancellationToken);

        var trustStatus = ComputeTrustStatus(hwidVerified, user.TrustScore, user.Status);

        var dto = MapToDto(user);
        dto.TrustStatus = trustStatus;

        return new LoginResponse
        {
            User = dto,
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            SessionId = session.SessionId,
            HwidVerified = hwidVerified,
            TrustStatus = trustStatus,
        };
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException("Username already taken");
        }

        if (!string.IsNullOrEmpty(request.HardwareId) && request.HardwareId != "unknown-hwid")
        {
            var hwidExists = await _db.Users.AnyAsync(u => u.HardwareId == request.HardwareId, cancellationToken);
            if (hwidExists)
            {
                _logger.LogWarning("Registration blocked: HWID already registered. HWID: {Hwid}", request.HardwareId);
                throw new InvalidOperationException("This PC is already registered to another account. Each computer can only have one account.");
            }
        }

        var user = new UserEntity
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName,
            Email = request.Email,
            Role = "player",
            TrustScore = 50,
            Level = 1,
            Xp = 0,
            NextLevelXp = 100,
            HardwareId = request.HardwareId,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered: {Username} ({UserId})", user.Username, user.Id);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, cancellationToken);

        var session = new SessionEntity { UserId = user.Id };
        _db.Sessions.Add(session);
        user.Status = "online";
        user.LastLoginAt = DateTime.UtcNow;
        CollectAndSaveHardwareInfo(user);
        await _db.SaveChangesAsync(cancellationToken);

        var hwidVerified = !string.IsNullOrEmpty(user.HardwareId);

        var dto = MapToDto(user);
        dto.TrustStatus = ComputeTrustStatus(hwidVerified, user.TrustScore, user.Status);

        return new LoginResponse
        {
            User = dto,
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            SessionId = session.SessionId,
            HwidVerified = hwidVerified,
            TrustStatus = dto.TrustStatus,
        };
    }

    public async Task<AuthTokens> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked, cancellationToken);

        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        stored.IsRevoked = true;

        var user = await _db.Users.FindAsync(new object[] { stored.UserId }, cancellationToken);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id, cancellationToken);

        var currentHwid = CollectCurrentHwid();
        CollectAndSaveHardwareInfo(user, currentHwid);

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthTokens
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token,
        };
    }

    public async Task LogoutAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User logged out: {UserId} session: {SessionId}", userId, sessionId);

        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId, cancellationToken);

        if (session != null)
        {
            session.IsActive = false;
        }

        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            user.Status = "offline";
        }

        await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        return MapToDto(user);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            _tokenHandler.ValidateToken(token, _validationParams, out _);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(string userId, DeviceRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Devices
            .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId, cancellationToken);

        if (existing != null)
        {
            existing.LastSeen = DateTime.UtcNow;
            existing.UserId ??= userId;
            await _db.SaveChangesAsync(cancellationToken);

            return new DeviceRegistrationResponse
            {
                Success = true,
                DeviceId = existing.DeviceId,
                TrustScore = existing.TrustScore,
                RequiresVerification = !existing.IsVerified,
            };
        }

        var fingerprintTrust = CalculateFingerprintTrust(request.Fingerprint);

        var device = new DeviceEntity
        {
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            OsVersion = request.OsVersion,
            Fingerprint = request.Fingerprint,
            TrustScore = fingerprintTrust,
            IsVerified = fingerprintTrust >= 70,
            UserId = userId,
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Device registered: {DeviceId} for user: {UserId}", request.DeviceId, userId);

        return new DeviceRegistrationResponse
        {
            Success = true,
            DeviceId = request.DeviceId,
            TrustScore = device.TrustScore,
            RequiresVerification = !device.IsVerified,
        };
    }

    public async Task<List<SessionInfo>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _db.Sessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionInfo
        {
            SessionId = s.SessionId,
            UserId = s.UserId,
            DeviceId = s.DeviceId,
            IpAddress = s.IpAddress,
            CreatedAt = s.CreatedAt,
            LastActivity = s.LastActivity,
            IsActive = s.IsActive,
        }).ToList();
    }

    public async Task<UserDto> UpdateProfileAsync(string userId, string? gamePath, string? hardwareId, string? serialNumber = null, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        if (gamePath != null) user.GamePath = gamePath;
        if (hardwareId != null)
        {
            var otherUser = await _db.Users.FirstOrDefaultAsync(u => u.HardwareId == hardwareId && u.Id != userId, cancellationToken);
            if (otherUser != null)
            {
                _logger.LogWarning("HWID linking blocked: HWID already bound to another user. HWID: {Hwid}, Target: {TargetId}, Owner: {OwnerId}",
                    hardwareId, userId, otherUser.Id);
                throw new InvalidOperationException("This PC is already registered to another account. Each computer can only have one account.");
            }
            user.HardwareId = hardwareId;
        }
        if (serialNumber != null) user.SerialNumber = serialNumber;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(user);
    }

    public async Task<HardwareVerificationResult> VerifyHardwareAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        var currentHwid = CollectCurrentHwid();
        var stored = !string.IsNullOrEmpty(user.HardwareId) && user.HardwareId != "unknown-hwid";

        return new HardwareVerificationResult
        {
            IsVerified = stored && currentHwid == user.HardwareId,
            HwidStored = stored,
            CurrentHwid = currentHwid,
            StoredHwid = user.HardwareId,
            Matches = stored && currentHwid == user.HardwareId,
        };
    }

    public async Task<string> GetTrustStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        var currentHwid = CollectCurrentHwid();
        var hwidVerified = !string.IsNullOrEmpty(user.HardwareId) && currentHwid == user.HardwareId;
        return ComputeTrustStatus(hwidVerified, user.TrustScore, user.Status);
    }

    public async Task TerminateSessionAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId, cancellationToken);

        if (session != null)
        {
            session.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Session terminated: {SessionId} for user: {UserId}", sessionId, userId);
        }
    }

    private string GenerateAccessToken(UserEntity user)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("trustScore", user.TrustScore.ToString()),
            new Claim("displayName", user.DisplayName ?? user.Username),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return _tokenHandler.WriteToken(token);
    }

    private async Task<RefreshTokenEntity> CreateRefreshTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var token = new RefreshTokenEntity
        {
            Token = GenerateRefreshToken(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
        };
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return token;
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static int CalculateFingerprintTrust(string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint)) return 30;
        var components = fingerprint.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var baseTrust = 40;
        baseTrust += Math.Min(components.Length * 10, 40);
        return Math.Min(baseTrust, 100);
    }

    private string? CollectCurrentHwid()
    {
        try
        {
            return _hardwareIdProvider.GetHardwareId();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect hardware ID");
            return null;
        }
    }

    private void CollectAndSaveHardwareInfo(UserEntity user, string? currentHwid = null)
    {
        if (!string.IsNullOrEmpty(user.HardwareId) && user.HardwareId != "unknown-hwid")
        {
            // HWID already bound
        }
        else if (!string.IsNullOrEmpty(currentHwid) && currentHwid != "unknown-hwid")
        {
            user.HardwareId = currentHwid;
        }
    }

    private static string ComputeTrustStatus(bool hwidVerified, int trustScore, string status)
    {
        if (status == "banned") return "restricted";
        if (hwidVerified && trustScore >= 50) return "trusted";
        return "pending";
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password changed for user {UserId}", userId);
        return true;
    }

    private static UserDto MapToDto(UserEntity user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role,
            TrustScore = user.TrustScore,
            TrustStatus = user.Status == "banned" ? "restricted"
                : (!string.IsNullOrEmpty(user.HardwareId) && user.HardwareId != "unknown-hwid" && user.TrustScore >= 50) ? "trusted"
                : "pending",
            Level = user.Level,
            Status = user.Status,
            Avatar = user.Avatar,
            Email = user.Email,
            Xp = user.Xp,
            NextLevelXp = user.NextLevelXp,
            HardwareId = user.HardwareId,
            SerialNumber = user.SerialNumber,
            GamePath = user.GamePath,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLoginAt ?? user.CreatedAt,
        };
    }
}
