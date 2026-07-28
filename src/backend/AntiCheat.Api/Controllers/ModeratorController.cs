using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "moderator,admin,superadmin")]
public class ModeratorController : ControllerBase
{
    private readonly IModeratorService _mod;
    private readonly IReportService _reports;
    private readonly ILogger<ModeratorController> _log;

    public ModeratorController(IModeratorService mod, IReportService reports, ILogger<ModeratorController> log)
    {
        _mod = mod;
        _reports = reports;
        _log = log;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _mod.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(CancellationToken ct)
    {
        var alerts = await _mod.GetAlertsAsync();
        return Ok(alerts);
    }

    [HttpGet("alerts/active")]
    public async Task<IActionResult> GetActiveAlerts(CancellationToken ct)
    {
        var alerts = await _mod.GetActiveAlertsAsync();
        return Ok(alerts);
    }

    [HttpPost("alerts/{alertId}/resolve")]
    public async Task<IActionResult> ResolveAlert(string alertId, CancellationToken ct)
    {
        await _mod.ResolveAlertAsync(alertId);
        return Ok(new { message = "Alert resolved" });
    }

    [HttpGet("players/search")]
    public async Task<IActionResult> SearchPlayers(
        [FromQuery] string? q,
        [FromQuery] string? email,
        [FromQuery] string? status,
        [FromQuery] int? minReports,
        [FromQuery] int? maxReports,
        CancellationToken ct)
    {
        var results = await _mod.SearchPlayersAsync(q, email, status, minReports, maxReports);
        return Ok(results);
    }

    [HttpGet("players/{id}")]
    public async Task<IActionResult> GetPlayerDetail(string id, CancellationToken ct)
    {
        var detail = await _mod.GetPlayerDetailAsync(id);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpGet("players/{playerId}/reports")]
    public async Task<IActionResult> GetPlayerReportList(string playerId, CancellationToken ct)
    {
        var reports = await _mod.GetPlayerReportsAsync(playerId);
        return Ok(reports);
    }

    // ---- Player Reports (chat system) ----

    [HttpGet("player-reports")]
    public async Task<IActionResult> GetPlayerReports([FromQuery] string? status, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var all = await _reports.GetMyReportsAsync(userId, ct);
        return Ok(all);
    }

    [HttpGet("player-reports/all")]
    public async Task<IActionResult> GetAllPlayerReports([FromQuery] string? playerId, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AntiCheat.Core.Data.AppDbContext>();
        var query = db.PlayerReports.Where(r => true);
        if (!string.IsNullOrWhiteSpace(playerId))
            query = query.Where(r => r.ReporterId == playerId || r.PlayerName == playerId);
        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PlayerReportDto
            {
                Id = r.Id,
                TicketType = r.TicketType,
                PlayerName = r.PlayerName,
                Reason = r.Reason,
                Description = r.Description,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Result = r.Result,
                ReporterId = r.ReporterId,
                ChatEnabled = r.ChatEnabled,
                IsFlagged = r.IsFlagged,
                AttachmentUrl = r.AttachmentUrl,
            })
            .ToListAsync(ct);
        return Ok(reports);
    }

    [HttpGet("player-reports/flagged")]
    public async Task<IActionResult> GetFlaggedPlayerReports([FromQuery] string? playerId, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AntiCheat.Core.Data.AppDbContext>();
        var query = db.PlayerReports.Where(r => r.IsFlagged);
        if (!string.IsNullOrWhiteSpace(playerId))
            query = query.Where(r => r.ReporterId == playerId || r.PlayerName == playerId);
        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PlayerReportDto
            {
                Id = r.Id,
                TicketType = r.TicketType,
                PlayerName = r.PlayerName,
                Reason = r.Reason,
                Description = r.Description,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Result = r.Result,
                ReporterId = r.ReporterId,
                ChatEnabled = r.ChatEnabled,
                IsFlagged = r.IsFlagged,
                AttachmentUrl = r.AttachmentUrl,
            })
            .ToListAsync(ct);
        return Ok(reports);
    }

    [HttpPut("player-reports/{id}/flag")]
    public async Task<IActionResult> FlagPlayerReport(string id, [FromBody] FlagReportRequest request, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AntiCheat.Core.Data.AppDbContext>();
        var report = await db.PlayerReports.FindAsync(new object[] { id }, ct);
        if (report == null) return NotFound();
        report.IsFlagged = request.IsFlagged;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, isFlagged = report.IsFlagged });
    }

    [HttpGet("player-reports/{id}")]
    public async Task<IActionResult> GetPlayerReport(string id, CancellationToken ct)
    {
        var report = await _reports.GetReportByIdAsync(id, ct);
        if (report == null) return NotFound();
        var msgs = await _reports.GetReportMessagesAsync(id, ct);
        report.Messages = msgs;
        return Ok(report);
    }

    [HttpGet("player-reports/{id}/messages")]
    public async Task<IActionResult> GetPlayerReportMessages(string id, CancellationToken ct)
    {
        var msgs = await _reports.GetReportMessagesAsync(id, ct);
        return Ok(new { messages = msgs });
    }

    [HttpPost("player-reports/{id}/messages")]
    public async Task<IActionResult> SendPlayerReportMessage(string id, [FromBody] SendReportMessageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? "Moderator";
        var msg = await _reports.SendReportMessageAsync(id, userId, userName, request.Message, null, ct);
        return Ok(new { success = true, message = msg });
    }

    [HttpPost("player-reports/{id}/messages/attachment")]
    public async Task<IActionResult> SendPlayerReportAttachment(string id, IFormFile file, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? "Moderator";
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var imageData = ms.ToArray();

        var cloudinary = HttpContext.RequestServices.GetRequiredService<ICloudinaryService>();
        var url = await cloudinary.UploadScreenshotAsync(imageData, file.FileName, "reports", ct);
        var msg = await _reports.SendReportMessageAsync(id, userId, userName, "", url, ct);
        return Ok(new { success = true, message = msg });
    }

    [HttpPut("player-reports/{id}/status")]
    public async Task<IActionResult> UpdatePlayerReportStatus(string id, [FromBody] UpdateReportStatusRequest request, CancellationToken ct)
    {
        var result = await _reports.UpdateReportStatusAsync(id, request.Status, ct);
        if (!result) return NotFound();
        return Ok(new { success = true, status = request.Status });
    }

    [HttpPut("player-reports/{id}/chat-toggle")]
    public async Task<IActionResult> TogglePlayerReportChat(string id, [FromBody] ToggleChatRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _reports.ToggleReportChatAsync(id, request.ChatEnabled, ct);
            if (!result)
            {
                _log.LogWarning("Chat toggle failed: report {ReportId} not found", id);
                return NotFound();
            }
            _log.LogInformation("Chat toggled for report {ReportId} to {ChatEnabled}", id, request.ChatEnabled);
            return Ok(new { success = true, chatEnabled = request.ChatEnabled });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error toggling chat for report {ReportId}", id);
            return StatusCode(500, new { error = "Failed to toggle chat" });
        }
    }
}
