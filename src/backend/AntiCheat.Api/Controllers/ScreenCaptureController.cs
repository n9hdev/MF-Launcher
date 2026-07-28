using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/screen")]
[Authorize(Roles = "moderator,admin,superadmin")]
public class ScreenCaptureController : ControllerBase
{
    private readonly IScreenCaptureService _captureService;
    private readonly IScreenStreamService _streamService;
    private readonly IEvidenceCollector _evidenceCollector;
    private readonly ICloudinaryService _cloudinary;
    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ScreenCaptureController> _logger;

    public ScreenCaptureController(
        IScreenCaptureService captureService,
        IScreenStreamService streamService,
        IEvidenceCollector evidenceCollector,
        ICloudinaryService cloudinary,
        AppDbContext db,
        IAuditLogService auditLog,
        ILogger<ScreenCaptureController> logger)
    {
        _captureService = captureService;
        _streamService = streamService;
        _evidenceCollector = evidenceCollector;
        _cloudinary = cloudinary;
        _db = db;
        _auditLog = auditLog;
        _logger = logger;
    }

    [HttpPost("capture")]
    public async Task<IActionResult> CaptureScreenshot([FromBody] ScreenshotRequest request)
    {
        var screenshot = await _captureService.CaptureAsync(request);
        return Ok(screenshot);
    }

    [HttpGet("capture/{playerId}")]
    public async Task<IActionResult> GetScreenshotHistory(string playerId, [FromQuery] int limit = 50)
    {
        var fromDb = await _db.Screenshots
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.CapturedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.PlayerId,
                s.DetectionEventId,
                s.Reason,
                s.CloudinaryUrl,
                s.FileSize,
                s.Status,
                s.CapturedAt,
                s.CapturedBy,
            })
            .ToListAsync();

        if (fromDb.Count > 0)
            return Ok(fromDb);

        var history = await _captureService.GetHistoryAsync(playerId, limit);
        return Ok(history);
    }

    [HttpGet("capture/detail/{id}")]
    public async Task<IActionResult> GetScreenshot(string id)
    {
        var screenshot = await _captureService.GetByIdAsync(id);
        if (screenshot == null) return NotFound();
        return Ok(screenshot);
    }

    [HttpPost("capture/{id}/sign")]
    public async Task<IActionResult> SignScreenshot(string id, [FromQuery] string secret)
    {
        var screenshot = await _captureService.GetByIdAsync(id);
        if (screenshot == null) return NotFound();

        var signature = _captureService.SignScreenshot(screenshot, secret);
        return Ok(new { id, signature });
    }

    [HttpPost("stream/create")]
    public async Task<IActionResult> CreateStream([FromBody] CreateStreamRequest request)
    {
        var session = await _streamService.CreateSessionAsync(request.PlayerId, request.DetectionEventId);
        return Ok(session);
    }

    [HttpGet("stream/active")]
    public async Task<IActionResult> GetActiveStreams()
    {
        var sessions = await _streamService.GetActiveSessionsAsync();
        return Ok(sessions);
    }

    [HttpGet("stream/{playerId}")]
    public async Task<IActionResult> GetStreamHistory(string playerId, [FromQuery] int limit = 20)
    {
        var fromDb = await _db.StreamSessions
            .Where(s => s.PlayerId == playerId)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.PlayerId,
                s.Status,
                s.TargetFps,
                s.TotalFrames,
                s.StartedBy,
                s.StartedAt,
                s.EndedAt,
                s.EndedReason,
            })
            .ToListAsync();

        if (fromDb.Count > 0)
            return Ok(fromDb);

        var history = await _streamService.GetHistoryAsync(playerId, limit);
        return Ok(history);
    }

    [HttpPost("stream/{sessionId}/end")]
    public async Task<IActionResult> EndStream(string sessionId)
    {
        var ended = await _streamService.EndSessionAsync(sessionId);
        if (!ended) return NotFound();

        try
        {
            var dbSession = await _db.StreamSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (dbSession != null)
            {
                dbSession.Status = "ended";
                dbSession.EndedAt = DateTime.UtcNow;
                dbSession.EndedReason = "admin-end";
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update stream session {SessionId} end status in DB", sessionId);
        }

        await _auditLog.LogAsync("stream_ended", User.Identity?.Name ?? "system", sessionId, "admin-end");

        return Ok(new { message = $"Stream {sessionId} ended" });
    }

    [HttpPost("stream/{sessionId}/fps")]
    public async Task<IActionResult> UpdateFps(string sessionId, [FromQuery] double fps)
    {
        var updated = await _streamService.UpdateFpsAsync(sessionId, fps);
        if (!updated) return NotFound();
        return Ok(new { sessionId, targetFps = fps });
    }

    [HttpPost("upload-proof")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadProof(IFormFile file, [FromQuery] string? playerId = null, [FromQuery] string? detectionEventId = null, [FromQuery] string? reason = null, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File exceeds 10MB limit" });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension)) extension = ".jpg";

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var fileName = $"proof_{playerId ?? "unknown"}_{timestamp}{extension}";

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var imageBytes = ms.ToArray();

        var folder = $"anticheat/proofs/{playerId ?? "unknown"}";
        var url = await _cloudinary.UploadScreenshotAsync(imageBytes, fileName, folder, ct);

        _logger.LogInformation("Proof uploaded for player {PlayerId}: {Url}", playerId, url);
        return Ok(new { url, fileName, playerId, detectionEventId, reason });
    }

    [HttpPost("stream/{sessionId}/link-evidence")]
    public async Task<IActionResult> LinkEvidence(string sessionId, [FromBody] LinkEvidenceRequest request)
    {
        var ev = new DetectionEventDto
        {
            Id = request.EventId,
            Type = "ScreenStream",
            Severity = "info",
            Description = $"Stream session {sessionId} linked to detection event",
        };

        var evidence = await _evidenceCollector.CaptureProcessSnapshotAsync(request.ProcessId, ev);
        return Ok(new { streamSessionId = sessionId, evidence });
    }
}

public class CreateStreamRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string? DetectionEventId { get; set; }
}

public class LinkEvidenceRequest
{
    public string EventId { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}
