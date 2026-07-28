using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _auth.RegisterAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            request.IpAddress ??= HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _auth.LoginAsync(request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _auth.RefreshTokenAsync(request.RefreshToken, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid refresh token" });
        }
    }

    [Authorize(Roles = "player,moderator,admin,superadmin")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _auth.LogoutAsync(userId, request.SessionId, ct);
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var user = await _auth.GetUserByIdAsync(userId, ct);
            return Ok(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }

    [Authorize]
    [HttpPost("devices/register")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _auth.RegisterDeviceAsync(userId, request, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var sessions = await _auth.GetActiveSessionsAsync(userId, ct);
        return Ok(sessions);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        try
        {
            var user = await _auth.GetUserByIdAsync(userId, ct);
            return Ok(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        try
        {
            var user = await _auth.UpdateProfileAsync(userId, request.GamePath, request.HardwareId, request.SerialNumber, ct);
            return Ok(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }

    [Authorize]
    [HttpPost("sessions/{sessionId}/terminate")]
    public async Task<IActionResult> TerminateSession(string sessionId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _auth.TerminateSessionAsync(userId, sessionId, ct);
        return Ok(new { message = "Session terminated" });
    }

    [Authorize]
    [HttpGet("bans/active")]
    public async Task<IActionResult> GetActiveBan(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var banService = HttpContext.RequestServices.GetRequiredService<BanService>();
        var ban = await banService.GetActiveBanAsync(userId, ct);
        if (ban == null)
            return Ok(new { banned = false });
        return Ok(new
        {
            banned = true,
            ban = new
            {
                ban.Id,
                ban.Reason,
                ban.Type,
                ban.IssuedBy,
                ban.IssuedAt,
                ban.ProofUrl,
                ban.DurationHours,
                ban.BannedAt,
            }
        });
    }

    [Authorize]
    [HttpPost("bans/{banId}/appeal")]
    public async Task<IActionResult> SubmitAppeal(string banId, [FromBody] SubmitAppealRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _auth.GetUserByIdAsync(userId, ct);
        var banService = HttpContext.RequestServices.GetRequiredService<BanService>();
        try
        {
            var appeal = await banService.SubmitAppealAsync(banId, userId, user.DisplayName, request.Message, ct);
            return Ok(new { success = true, appealId = appeal.Id, appeal });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Ban not found" });
        }
    }

    [Authorize]
    [HttpGet("bans/appeal")]
    public async Task<IActionResult> GetMyAppeal(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var banService = HttpContext.RequestServices.GetRequiredService<BanService>();
        var appeal = await banService.GetPlayerAppealAsync(userId, ct);
        if (appeal == null)
            return Ok(new { hasAppeal = false });
        var messages = await banService.GetAppealMessagesAsync(appeal.Id, ct);
        return Ok(new
        {
            hasAppeal = true,
            appeal = new
            {
                appeal.Id,
                appeal.Player,
                appeal.PlayerId,
                appeal.BanId,
                appeal.Reason,
                appeal.BanType,
                appeal.Status,
                appeal.Date,
                appeal.Reviewer,
                Messages = messages.Select(m => new
                {
                    m.Id, m.AppealId, m.SenderId, m.SenderName, m.Message, m.CreatedAt,
                }).ToList(),
            }
        });
    }

    [Authorize]
    [HttpPost("bans/appeal/messages")]
    public async Task<IActionResult> SendAppealMessage([FromBody] AntiCheat.Shared.Models.SendAppealMessageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _auth.GetUserByIdAsync(userId, ct);
        var banService = HttpContext.RequestServices.GetRequiredService<BanService>();
        var appeal = await banService.GetPlayerAppealAsync(userId, ct);
        if (appeal == null)
            return NotFound(new { error = "No appeal found" });
        var msg = await banService.SendAppealMessageAsync(appeal.Id, userId, user.DisplayName, request.Message, ct);
        return Ok(new { success = true, message = msg });
    }

    [Authorize]
    [HttpGet("trust-status")]
    public async Task<IActionResult> GetTrustStatus(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        try
        {
            var trustStatus = await _auth.GetTrustStatusAsync(userId, ct);
            return Ok(new { trustStatus });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        if (!result)
            return BadRequest(new { error = "Current password is incorrect" });
        return Ok(new { success = true });
    }

    [Authorize]
    [HttpGet("identity")]
    public async Task<IActionResult> GetIdentity(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _auth.GetUserByIdAsync(userId, ct);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(new
        {
            ip,
            hardwareId = user.HardwareId,
            serialNumber = user.SerialNumber,
        });
    }

    [Authorize]
    [HttpGet("verify-hardware")]
    public async Task<IActionResult> VerifyHardware(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        try
        {
            var result = await _auth.VerifyHardwareAsync(userId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
    }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public string? GamePath { get; set; }
    public string? HardwareId { get; set; }
    public string? SerialNumber { get; set; }
}

public class SubmitAppealRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}