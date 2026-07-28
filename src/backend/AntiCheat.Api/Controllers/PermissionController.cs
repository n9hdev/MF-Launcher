using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissions;

    public PermissionController(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _permissions.GetUserPermissionsResponseAsync(userId, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllPermissions(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _permissions.GetAllPermissionsAsync(userId, ct);
        return Ok(result);
    }

    [HttpPost("check")]
    public async Task<IActionResult> CheckPermission([FromBody] PermissionCheckRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var granted = await _permissions.HasPermissionAsync(userId, request.Permission, ct);
        return Ok(new PermissionCheckResponse
        {
            Permission = request.Permission,
            Granted = granted,
        });
    }

    [HttpGet("flags")]
    public async Task<IActionResult> GetFeatureFlags(CancellationToken ct)
    {
        var flags = await _permissions.GetFeatureFlagsAsync(ct);
        return Ok(flags);
    }

    [HttpGet("flags/{key}")]
    public async Task<IActionResult> GetFeatureFlag(string key, CancellationToken ct)
    {
        var enabled = await _permissions.IsFeatureEnabledAsync(key, ct);
        return Ok(new { Key = key, Enabled = enabled });
    }
}
