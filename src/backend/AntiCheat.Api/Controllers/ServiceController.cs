using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AntiCheat.Api.Hubs;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<AntiCheatHub> _hubContext;
    private readonly IHubContext<ScreenStreamHub> _screenHubContext;
    private readonly IVerdictService _verdictService;
    private readonly IDedupService _dedupService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(
        AppDbContext db,
        IMemoryCache cache,
        IHubContext<AntiCheatHub> hubContext,
        IHubContext<ScreenStreamHub> screenHubContext,
        IVerdictService verdictService,
        IDedupService dedupService,
        IServiceScopeFactory scopeFactory,
        IAuditLogService auditLog,
        ILogger<ServiceController> logger)
    {
        _db = db;
        _cache = cache;
        _hubContext = hubContext;
        _screenHubContext = screenHubContext;
        _verdictService = verdictService;
        _dedupService = dedupService;
        _scopeFactory = scopeFactory;
        _auditLog = auditLog;
        _logger = logger;
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ServiceHeartbeatResponse>> Heartbeat([FromBody] ServiceHeartbeatRequest request)
    {
        var response = new ServiceHeartbeatResponse();

        if (string.IsNullOrWhiteSpace(request.HardwareId))
            return Ok(response);

        // If the Service included a UserId, look up by ID and cross-validate/link HWID
        UserEntity? user = null;
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            user = await _db.Users.FindAsync(new object[] { request.UserId });

            if (user != null)
            {
                if (string.IsNullOrEmpty(user.HardwareId) || user.HardwareId == "unknown-hwid")
                {
                    // First time this PC's Service connects — auto-link the real HWID
                    _logger.LogInformation("Auto-linking HWID {Hwid} to user {UserId} ({Username})",
                        request.HardwareId, user.Id, user.Username);
                    user.HardwareId = request.HardwareId;
                }
                else if (user.HardwareId != request.HardwareId)
                {
                    // Stored HWID exists but doesn't match Service HWID → spoofing
                    _logger.LogWarning("HWID SPOOFING DETECTED for user {UserId} ({Username}): stored HWID {StoredHwid} != service HWID {ServiceHwid}",
                        user.Id, user.Username, user.HardwareId, request.HardwareId);

                    using var scope = _scopeFactory.CreateScope();
                    var banService = scope.ServiceProvider.GetRequiredService<BanService>();
                    await banService.AutoBanAsync(
                        playerId: user.Id,
                        playerName: user.Username,
                        reason: $"HWID spoofing detected: stored HWID ({user.HardwareId}) differs from actual machine HWID ({request.HardwareId}). The hwid.txt file was tampered with.",
                        issuedBy: "System",
                        banType: "Permanent",
                        proofUrl: null);

                    user.Status = "banned";
                    await _db.SaveChangesAsync();

                    response.Commands.Add("player_banned");
                    return Ok(response);
                }
            }
        }

        // Fall back to HWID-only lookup if UserId wasn't provided or user not found
        user ??= await _db.Users.FirstOrDefaultAsync(u => u.HardwareId == request.HardwareId);
        if (user != null)
        {
            if (user.Status == "banned")
            {
                response.Commands.Add("player_banned");
                return Ok(response);
            }

            user.LastLoginAt = DateTime.UtcNow;
            user.Status = request.GameRunning ? "playing" : "online";
            if (!string.IsNullOrEmpty(request.SerialNumber))
                user.SerialNumber = request.SerialNumber;
            if (!string.IsNullOrEmpty(request.GamePath))
                user.GamePath = request.GamePath;
            await _db.SaveChangesAsync();

            bool needsPreLaunch = !_cache.TryGetValue($"prelaunch_{request.HardwareId}", out _);
            if (needsPreLaunch)
                response.Commands.Add("run_prelaunch_scan");

            if (_cache.TryGetValue<string>($"take_screenshot_{request.HardwareId}", out var screenshotRequestId))
            {
                response.Commands.Add("take_screenshot");
                response.CommandData["screenshotRequestId"] = screenshotRequestId;
                _cache.Remove($"take_screenshot_{request.HardwareId}");
            }

            if (_cache.TryGetValue<string>($"start_stream_{request.HardwareId}", out var streamSessionJson))
            {
                response.Commands.Add("start_stream");
                response.CommandData["streamSession"] = streamSessionJson;
                _cache.Remove($"start_stream_{request.HardwareId}");
            }

            if (_cache.TryGetValue<string>($"stop_stream_{request.HardwareId}", out _))
            {
                response.Commands.Add("stop_stream");
                _cache.Remove($"stop_stream_{request.HardwareId}");
            }

            _cache.Set($"last_heartbeat_{request.HardwareId}", request, TimeSpan.FromMinutes(2));
        }

        return Ok(response);
    }

    [HttpPost("detections")]
    public async Task<ActionResult<ServiceDetectionResponse>> ReportDetections([FromBody] ServiceDetectionReport request)
    {
        string? playerId = null;
        string? playerName = null;

        if (!string.IsNullOrWhiteSpace(request.HardwareId))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.HardwareId == request.HardwareId);
            if (user != null)
            {
                playerId = user.Id;
                playerName = user.Username;
            }
        }

        var deduped = await _dedupService.DeduplicateAsync(request.Detections);
        var response = new ServiceDetectionResponse { Received = true, Count = deduped.Count };

        // Cache prelaunch results even when empty — prevents infinite re-run loop
        if (request.ScanType == "prelaunch" && !string.IsNullOrWhiteSpace(request.HardwareId))
        {
            _cache.Set($"prelaunch_{request.HardwareId}", deduped.ToList(), TimeSpan.FromMinutes(5));

            if (playerId != null)
            {
                await _hubContext.Clients.Group($"user_{playerId}").SendAsync("PreLaunchResults", deduped);
                await _hubContext.Clients.Group($"user_{playerId}").SendAsync("GameLaunchUnlocked");
            }
        }

        if (deduped.Count == 0)
            return Ok(response);

        foreach (var detection in deduped)
        {
            _db.DetectionEvents.Add(new DetectionEventEntity
            {
                Id = detection.Id,
                Type = detection.Type,
                Severity = detection.Severity,
                Description = detection.Description,
                Confidence = detection.Confidence,
                EvidencePath = detection.EvidencePath,
                PlayerId = playerId,
                ProcessName = detection.ProcessName,
                Timestamp = detection.Timestamp,
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Saved {Count} detection events from HWID {HwId} (PlayerId: {PlayerId})",
            deduped.Count, request.HardwareId, playerId ?? "unknown");

        await EvaluateVerdictAndMaybeBanAsync(deduped, playerId, playerName, request.HardwareId);

        return Ok(response);
    }

    private async Task EvaluateVerdictAndMaybeBanAsync(IReadOnlyList<DetectionEventDto> detections, string? playerId, string? playerName, string hardwareId)
    {
        if (detections.Count == 0)
            return;

        try
        {
            var verdict = await _verdictService.EvaluateAsync(detections, playerId);

            _logger.LogInformation("Verdict for HWID {HwId}: {Verdict} (confidence: {Conf}, action: {Action})",
                hardwareId, verdict.Verdict, verdict.FinalConfidence, verdict.SuggestedAction);

            if (verdict.SuggestedAction != "ban" || playerId == null || playerName == null)
                return;

            using var scope = _scopeFactory.CreateScope();
            var banService = scope.ServiceProvider.GetRequiredService<BanService>();

            if (await banService.IsPlayerBannedAsync(playerId))
            {
                _logger.LogWarning("Auto-ban skipped: {Player} ({Id}) already has active ban", playerName, playerId);
                return;
            }

            string? proofUrl = null;
            try
            {
                var screenCapture = scope.ServiceProvider.GetRequiredService<IScreenCaptureService>();
                using var screenshotCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var screenshot = await screenCapture.CaptureAsync(
                    new ScreenshotRequest { PlayerId = playerId, Reason = verdict.Explanation },
                    screenshotCts.Token);
                if (!string.IsNullOrEmpty(screenshot.StoragePath) &&
                    (screenshot.StoragePath.StartsWith("http://") || screenshot.StoragePath.StartsWith("https://")))
                {
                    proofUrl = screenshot.StoragePath;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Screenshot capture timed out for {Id}, proceeding with ban", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screenshot capture failed before auto-ban for {Id}", playerId);
            }

            var banEntry = await banService.AutoBanAsync(
                playerId, playerName,
                verdict.Explanation,
                issuedBy: "AutoBan",
                banType: verdict.EscalationRequired ? "Permanent" : "Temporary",
                proofUrl: proofUrl);

            _logger.LogWarning("Auto-ban issued: {Player} ({Id}) - {Reason}", playerName, playerId, verdict.Explanation);

            try
            {
                var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notifier.NotifyBanAsync(playerId, new
                {
                    id = banEntry.Id,
                    reason = banEntry.Reason,
                    type = banEntry.Type,
                    issuedBy = banEntry.IssuedBy,
                    issuedAt = banEntry.IssuedAt,
                    proofUrl = banEntry.ProofUrl,
                    durationHours = banEntry.DurationHours,
                    bannedAt = banEntry.BannedAt.ToString("O"),
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send ban notification to player {Id}", playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-ban evaluation failed for HWID {HwId}", hardwareId);
        }
    }

    [HttpPost("verify-hashes")]
    public async Task<ActionResult<GameHashVerifyResponse>> VerifyGameHashes([FromBody] GameHashVerifyRequest request)
    {
        try
        {
            var response = new GameHashVerifyResponse();
            foreach (var file in request.Files)
            {
                var existing = await _db.GameFileHashes
                    .FirstOrDefaultAsync(h => h.FilePath == file.FilePath);

                if (existing == null)
                {
                    _db.GameFileHashes.Add(new GameFileHashEntity
                    {
                        FilePath = file.FilePath,
                        Sha256 = file.Sha256,
                        Md5 = file.Md5,
                        FileSize = file.FileSize,
                        FileName = Path.GetFileName(file.FilePath),
                        FirstSeen = DateTime.UtcNow,
                        LastVerified = DateTime.UtcNow,
                        VerificationCount = 1,
                    });
                    response.Results.Add(new GameFileHashResult
                    {
                        FilePath = file.FilePath,
                        Status = "new",
                    });
                }
                else
                {
                    existing.LastVerified = DateTime.UtcNow;
                    existing.VerificationCount++;

                    if (!string.Equals(existing.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        response.Results.Add(new GameFileHashResult
                        {
                            FilePath = file.FilePath,
                            Status = "mismatch",
                            ExpectedSha256 = existing.Sha256,
                            ExpectedFileSize = existing.FileSize,
                        });
                    }
                    else
                    {
                        response.Results.Add(new GameFileHashResult
                        {
                            FilePath = file.FilePath,
                            Status = "match",
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Game hash verification failed");
            return StatusCode(500, new GameHashVerifyResponse());
        }
    }

    [HttpPost("screenshot-upload")]
    public async Task<ActionResult<ServiceScreenshotUploadResponse>> UploadScreenshot(
        [FromBody] ServiceScreenshotUploadRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ImageBase64))
                return Ok(new ServiceScreenshotUploadResponse { Success = false, Error = "No image data" });

            var imageBytes = Convert.FromBase64String(request.ImageBase64);

            string? playerId = request.PlayerId;
            if (string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(request.HardwareId))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.HardwareId == request.HardwareId);
                playerId = user?.Id;
            }

            var screenshotId = Guid.NewGuid().ToString("N");
            var cloudUrl = (string?)null;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cloudinary = scope.ServiceProvider.GetRequiredService<ICloudinaryService>();
                var fileName = $"{screenshotId}.{request.Format}";
                var folder = $"anticheat/screenshots/{playerId ?? "unknown"}";
                cloudUrl = await cloudinary.UploadScreenshotAsync(imageBytes, fileName, folder);
                _logger.LogInformation("Service screenshot uploaded to Cloudinary: {Url}", cloudUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary upload failed for service screenshot");
            }

            var capture = new ScreenshotCaptureDto
            {
                Id = screenshotId,
                PlayerId = playerId ?? request.HardwareId,
                DetectionEventId = request.DetectionEventId,
                ImageData = imageBytes,
                Format = request.Format,
                CapturedAt = request.CapturedAt,
                CapturedBy = "service",
                StoragePath = cloudUrl,
            };

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var screenCaptureService = scope.ServiceProvider.GetRequiredService<IScreenCaptureService>();
                await screenCaptureService.StoreScreenshotAsync(capture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store screenshot in memory cache");
            }

            try
            {
                _db.Screenshots.Add(new ScreenshotEntity
                {
                    Id = screenshotId,
                    PlayerId = playerId ?? request.HardwareId,
                    HardwareId = request.HardwareId,
                    DetectionEventId = request.DetectionEventId,
                    Reason = request.Reason ?? "service-capture",
                    CloudinaryUrl = cloudUrl ?? "",
                    FileSize = imageBytes.Length,
                    RequestId = request.RequestId ?? screenshotId,
                    Status = "captured",
                    CapturedAt = request.CapturedAt == default ? DateTime.UtcNow : request.CapturedAt,
                    CapturedBy = "service",
                });
                await _db.SaveChangesAsync();
                _logger.LogInformation("Screenshot {Id} persisted to database for player {PlayerId}", screenshotId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist screenshot {Id} to database", screenshotId);
            }

            if (!string.IsNullOrEmpty(playerId))
            {
                await _hubContext.Clients.Group($"user_{playerId}").SendAsync("ScreenshotCaptured", new
                {
                    screenshotId,
                    playerId,
                    detectionEventId = request.DetectionEventId,
                    cloudUrl,
                    capturedAt = request.CapturedAt,
                });
            }

            return Ok(new ServiceScreenshotUploadResponse
            {
                Success = true,
                CloudUrl = cloudUrl,
                ScreenshotId = screenshotId,
            });
        }
        catch (FormatException)
        {
            return Ok(new ServiceScreenshotUploadResponse { Success = false, Error = "Invalid base64 data" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process screenshot upload from service");
            return Ok(new ServiceScreenshotUploadResponse { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("stream-frame")]
    public async Task<ActionResult<ServiceStreamFrameResponse>> SubmitStreamFrame(
        [FromBody] ServiceStreamFrameRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ImageBase64))
                return Ok(new ServiceStreamFrameResponse { Success = false });

            var imageBytes = Convert.FromBase64String(request.ImageBase64);

            using var scope = _scopeFactory.CreateScope();
            var streamService = scope.ServiceProvider.GetRequiredService<IScreenStreamService>();
            var session = await streamService.GetSessionAsync(request.SessionId);

            if (session == null || session.Status != "active")
                return Ok(new ServiceStreamFrameResponse { Success = false, ShouldStop = true });

            var frameNum = await streamService.IncrementFrameCountAsync(request.SessionId);

            try
            {
                var dbSession = await _db.StreamSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId);
                if (dbSession != null)
                {
                    dbSession.TotalFrames = frameNum;
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update stream session {SessionId} frame count in DB", request.SessionId);
            }

            var frame = new ScreenFrameDto
            {
                SessionId = request.SessionId,
                FrameNumber = frameNum,
                ImageData = imageBytes,
                Width = request.Width,
                Height = request.Height,
                Format = request.Format,
                Timestamp = request.Timestamp,
            };

            await _screenHubContext.Clients.Group($"stream_{request.SessionId}").SendAsync("FrameReceived", new
            {
                sessionId = frame.SessionId,
                frameNumber = frame.FrameNumber,
                imageData = Convert.ToBase64String(frame.ImageData),
                width = frame.Width,
                height = frame.Height,
                format = frame.Format,
                timestamp = frame.Timestamp,
            });

            return Ok(new ServiceStreamFrameResponse
            {
                Success = true,
                FrameNumber = frameNum,
                TargetFps = session.TargetFps,
                ShouldStop = false,
            });
        }
        catch (FormatException)
        {
            return Ok(new ServiceStreamFrameResponse { Success = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process stream frame from service");
            return Ok(new ServiceStreamFrameResponse { Success = false });
        }
    }

    [HttpPost("request-screenshot/{playerId}")]
    public async Task<IActionResult> RequestScreenshot(string playerId, [FromQuery] string? reason = null)
    {
        var user = await _db.Users.FindAsync(playerId);
        if (user == null || string.IsNullOrEmpty(user.HardwareId))
            return NotFound(new { error = "Player not found or no hardware ID linked" });

        var requestId = Guid.NewGuid().ToString("N");
        _cache.Set($"take_screenshot_{user.HardwareId}", requestId, TimeSpan.FromSeconds(120));
        _logger.LogInformation("Queued take_screenshot command for player {PlayerId} (HWID: {Hwid}, request: {RequestId})",
            playerId, user.HardwareId, requestId);

        _db.Screenshots.Add(new ScreenshotEntity
        {
            Id = requestId,
            PlayerId = playerId,
            HardwareId = user.HardwareId,
            Reason = reason ?? "admin-request",
            RequestId = requestId,
            Status = "pending",
            CapturedAt = DateTime.UtcNow,
            CapturedBy = User.Identity?.Name ?? "admin",
        });
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("screenshot_requested", User.Identity?.Name ?? "system", playerId, reason ?? "admin request");

        return Ok(new { requestId, playerId, hardwareId = user.HardwareId });
    }

    [HttpPost("start-stream/{playerId}")]
    public async Task<IActionResult> RequestStreamStart(string playerId, [FromQuery] string? detectionEventId = null)
    {
        var user = await _db.Users.FindAsync(playerId);
        if (user == null || string.IsNullOrEmpty(user.HardwareId))
            return NotFound(new { error = "Player not found or no hardware ID linked" });

        using var scope = _scopeFactory.CreateScope();
        var streamService = scope.ServiceProvider.GetRequiredService<IScreenStreamService>();
        var session = await streamService.CreateSessionAsync(playerId, detectionEventId);

        _db.StreamSessions.Add(new StreamSessionEntity
        {
            Id = session.SessionId,
            PlayerId = playerId,
            HardwareId = user.HardwareId,
            Status = "active",
            TargetFps = session.TargetFps,
            JpegQuality = session.JpegQuality,
            StartedBy = User.Identity?.Name ?? "admin",
            StartedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var sessionJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            session.SessionId,
            session.TargetFps,
            session.JpegQuality,
        });

        _cache.Set($"start_stream_{user.HardwareId}", sessionJson, TimeSpan.FromSeconds(120));
        _logger.LogInformation("Queued start_stream command for player {PlayerId} (HWID: {Hwid}, session: {SessionId})",
            playerId, user.HardwareId, session.SessionId);

        await _auditLog.LogAsync("stream_started", User.Identity?.Name ?? "system", playerId, $"session={session.SessionId}");

        return Ok(new { session.SessionId, session.PlayerId, session.TargetFps, session.JpegQuality });
    }

    [HttpPost("stop-stream/{playerId}")]
    public async Task<IActionResult> RequestStreamStop(string playerId)
    {
        var user = await _db.Users.FindAsync(playerId);
        if (user == null || string.IsNullOrEmpty(user.HardwareId))
            return NotFound(new { error = "Player not found or no hardware ID linked" });

        _cache.Set($"stop_stream_{user.HardwareId}", "stop", TimeSpan.FromSeconds(120));

        try
        {
            var activeSession = await _db.StreamSessions
                .Where(s => s.PlayerId == playerId && s.Status == "active")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();
            if (activeSession != null)
            {
                activeSession.Status = "stopped";
                activeSession.EndedAt = DateTime.UtcNow;
                activeSession.EndedReason = "admin-stop";
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update stream session status in DB for player {PlayerId}", playerId);
        }

        _logger.LogInformation("Queued stop_stream command for player {PlayerId} (HWID: {Hwid})", playerId, user.HardwareId);

        await _auditLog.LogAsync("stream_stopped", User.Identity?.Name ?? "system", playerId, "admin-stop");

        return Ok(new { playerId });
    }
}
