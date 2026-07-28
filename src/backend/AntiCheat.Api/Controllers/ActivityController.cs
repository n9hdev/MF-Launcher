using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activity;

    public ActivityController(IActivityService activity)
    {
        _activity = activity;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var activities = await _activity.GetRecentActivityAsync(GetUserId(), count, ct);
        return Ok(activities);
    }
}
