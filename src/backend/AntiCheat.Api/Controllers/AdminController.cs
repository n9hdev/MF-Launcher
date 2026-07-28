using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,superadmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        return Ok(await _admin.GetStatsAsync());
    }

    [HttpGet("detectors")]
    public async Task<IActionResult> GetDetectorPerformance(CancellationToken ct)
    {
        return Ok(await _admin.GetDetectorPerformanceAsync());
    }

    [HttpGet("bans")]
    public async Task<IActionResult> GetBans(CancellationToken ct)
    {
        return Ok(await _admin.GetBansAsync());
    }

    [HttpGet("bans/{id}")]
    public async Task<IActionResult> GetBanById(string id, CancellationToken ct)
    {
        var ban = await _admin.GetBanByIdAsync(id);
        if (ban == null) return NotFound();
        return Ok(ban);
    }

    [HttpPost("bans")]
    public async Task<IActionResult> CreateBan([FromBody] CreateBanRequest request, CancellationToken ct)
    {
        var ban = await _admin.CreateBanAsync(request);
        return CreatedAtAction(nameof(GetBanById), new { id = ban.Id }, ban);
    }

    [HttpPut("bans/{id}")]
    public async Task<IActionResult> UpdateBan(string id, [FromBody] UpdateBanRequest request, CancellationToken ct)
    {
        var ban = await _admin.UpdateBanAsync(id, request);
        if (ban == null) return NotFound();
        return Ok(ban);
    }

    [HttpDelete("bans/{id}")]
    public async Task<IActionResult> RevokeBan(string id, CancellationToken ct)
    {
        var result = await _admin.RevokeBanAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpGet("appeals")]
    public async Task<IActionResult> GetAppeals(CancellationToken ct)
    {
        return Ok(await _admin.GetAppealsAsync());
    }

    [HttpGet("appeals/{id}")]
    public async Task<IActionResult> GetAppealById(string id, CancellationToken ct)
    {
        var appeal = await _admin.GetAppealByIdAsync(id);
        if (appeal == null) return NotFound();
        return Ok(appeal);
    }

    [HttpPut("appeals/{id}")]
    public async Task<IActionResult> UpdateAppealStatus(string id, [FromBody] UpdateAppealStatusRequest request, CancellationToken ct)
    {
        var appeal = await _admin.UpdateAppealStatusAsync(id, request.Status, request.Reviewer);
        if (appeal == null) return NotFound();
        return Ok(appeal);
    }

    [HttpGet("appeals/{id}/messages")]
    public async Task<IActionResult> GetAppealMessages(string id, CancellationToken ct)
    {
        var messages = await _admin.GetAppealMessagesAsync(id);
        return Ok(new { messages });
    }

    [HttpPost("appeals/{id}/messages")]
    public async Task<IActionResult> SendAppealMessage(string id, [FromBody] SendAppealMessageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "system";
        var userName = User.Identity?.Name ?? "System";
        var msg = await _admin.SendAppealReplyAsync(id, userId, userName, request.Message);
        return Ok(msg);
    }

    [HttpGet("whitelist")]
    public async Task<IActionResult> GetWhitelist(CancellationToken ct)
    {
        return Ok(await _admin.GetWhitelistAsync());
    }

    [HttpPost("whitelist")]
    public async Task<IActionResult> AddWhitelistEntry([FromBody] AddWhitelistEntryRequest request, CancellationToken ct)
    {
        var entry = await _admin.AddWhitelistEntryAsync(request);
        return CreatedAtAction(nameof(GetWhitelist), new { id = entry.Id }, entry);
    }

    [HttpPut("whitelist/{id}")]
    public async Task<IActionResult> UpdateWhitelistEntry(string id, [FromBody] UpdateWhitelistEntryRequest request, CancellationToken ct)
    {
        var entry = await _admin.UpdateWhitelistEntryAsync(id, request);
        if (entry == null) return NotFound();
        return Ok(entry);
    }

    [HttpDelete("whitelist/{id}")]
    public async Task<IActionResult> RemoveWhitelistEntry(string id, CancellationToken ct)
    {
        var result = await _admin.RemoveWhitelistEntryAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpGet("analytics/weekly")]
    public async Task<IActionResult> GetWeeklyActivity(CancellationToken ct)
    {
        return Ok(await _admin.GetWeeklyActivityAsync());
    }

    [HttpGet("analytics/threats")]
    public async Task<IActionResult> GetThreatDistribution(CancellationToken ct)
    {
        return Ok(await _admin.GetThreatDistributionAsync());
    }

    [HttpGet("analytics/top-reports")]
    public async Task<IActionResult> GetTopReports(CancellationToken ct)
    {
        return Ok(await _admin.GetTopReportsAsync());
    }
}
