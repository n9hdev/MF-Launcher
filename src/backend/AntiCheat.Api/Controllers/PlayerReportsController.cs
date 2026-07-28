using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class PlayerReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public PlayerReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyReports(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var reports = await _reports.GetMyReportsAsync(userId, ct);
        return Ok(reports);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitReport([FromBody] ReportSubmissionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var report = await _reports.SubmitReportAsync(userId, request, ct);
        return Ok(report);
    }

    [HttpPost("upload-attachment")]
    public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });
        if (file.Length > 20 * 1024 * 1024)
            return BadRequest(new { error = "File size exceeds 20 MB limit" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var imageData = ms.ToArray();

        var cloudinary = HttpContext.RequestServices.GetRequiredService<ICloudinaryService>();
        var url = await cloudinary.UploadScreenshotAsync(imageData, file.FileName, "reports", ct);
        return Ok(new { url });
    }

    [HttpGet("{reportId}")]
    public async Task<IActionResult> GetReport(string reportId, CancellationToken ct)
    {
        var report = await _reports.GetReportByIdAsync(reportId, ct);
        if (report == null) return NotFound(new { error = "Report not found" });
        return Ok(report);
    }

    [HttpGet("{reportId}/messages")]
    public async Task<IActionResult> GetMessages(string reportId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var report = await _reports.GetReportByIdAsync(reportId, ct);
        if (report == null) return NotFound(new { error = "Report not found" });
        if (report.ReporterId != userId && !User.IsInRole("moderator") && !User.IsInRole("admin") && !User.IsInRole("superadmin"))
            return Forbid();
        var msgs = await _reports.GetReportMessagesAsync(reportId, ct);
        return Ok(new { messages = msgs });
    }

    [HttpPost("{reportId}/messages")]
    public async Task<IActionResult> SendMessage(string reportId, [FromBody] SendReportMessageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? "Unknown";
        var report = await _reports.GetReportByIdAsync(reportId, ct);
        if (report == null) return NotFound(new { error = "Report not found" });
        if (report.ReporterId != userId && !User.IsInRole("moderator") && !User.IsInRole("admin") && !User.IsInRole("superadmin"))
            return Forbid();
        if (!report.ChatEnabled && report.ReporterId == userId)
            return BadRequest(new { error = "Chat is disabled for this report" });
        var msg = await _reports.SendReportMessageAsync(reportId, userId, userName, request.Message, null, ct);
        return Ok(new { success = true, message = msg });
    }

    [HttpPost("{reportId}/messages/attachment")]
    public async Task<IActionResult> SendAttachment(string reportId, IFormFile file, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? "Unknown";
        var report = await _reports.GetReportByIdAsync(reportId, ct);
        if (report == null) return NotFound(new { error = "Report not found" });
        if (report.ReporterId != userId && !User.IsInRole("moderator") && !User.IsInRole("admin") && !User.IsInRole("superadmin"))
            return Forbid();
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var imageData = ms.ToArray();

        var cloudinary = HttpContext.RequestServices.GetRequiredService<ICloudinaryService>();
        var url = await cloudinary.UploadScreenshotAsync(imageData, file.FileName, "reports", ct);
        var msg = await _reports.SendReportMessageAsync(reportId, userId, userName, "", url, ct);
        return Ok(new { success = true, message = msg });
    }
}
