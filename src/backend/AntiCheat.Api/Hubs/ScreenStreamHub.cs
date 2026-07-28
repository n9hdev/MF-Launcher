using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Api.Hubs;

[Authorize(Roles = "moderator,admin,superadmin")]
public class ScreenStreamHub : Hub
{
    private readonly IScreenStreamService _streamService;
    private readonly AppDbContext _db;
    private readonly ILogger<ScreenStreamHub> _logger;
    private static readonly ConcurrentDictionary<string, string> _connectionToSession = new();
    private static readonly ConcurrentDictionary<string, DateTime> _lastFrameTime = new();
    private static readonly ScreenStreamConfigDto _config = new();

    public ScreenStreamHub(IScreenStreamService streamService, AppDbContext db, ILogger<ScreenStreamHub> logger)
    {
        _streamService = streamService;
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("ScreenStream client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("ScreenStream client disconnected: {ConnectionId}", Context.ConnectionId);

        _lastFrameTime.TryRemove(Context.ConnectionId, out _);

        if (_connectionToSession.TryRemove(Context.ConnectionId, out var sessionId))
        {
            var adminId = Context.UserIdentifier ?? "unknown";
            await _streamService.RemoveViewerAsync(sessionId, adminId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream_{sessionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartStream(string sessionId)
    {
        var session = await _streamService.GetSessionAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("StreamError", "Session not found");
            return;
        }

        _connectionToSession[Context.ConnectionId] = sessionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream_{sessionId}");
        _logger.LogInformation("Client {ConnectionId} started streaming for session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    public async Task SubmitFrame(ScreenFrameDto frame)
    {
        var now = DateTime.UtcNow;

        if (_lastFrameTime.TryGetValue(Context.ConnectionId, out var last))
        {
            var elapsed = (now - last).TotalMilliseconds;
            var minInterval = 1000.0 / _config.FrameRateLimitPerSecond;
            if (elapsed < minInterval)
            {
                _logger.LogWarning("Frame rate limit exceeded for {ConnectionId}: {Elapsed:F0}ms < {Min:F0}ms",
                    Context.ConnectionId, elapsed, minInterval);
                return;
            }
        }

        _lastFrameTime[Context.ConnectionId] = now;

        if (!_connectionToSession.TryGetValue(Context.ConnectionId, out var sessionId))
        {
            _logger.LogWarning("Frame from unregistered connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("StreamError", "Not registered in any stream session");
            return;
        }

        frame.SessionId = sessionId;
        frame.Timestamp = now;

        var frameNum = await _streamService.IncrementFrameCountAsync(sessionId);
        frame.FrameNumber = frameNum;

        await Clients.Group($"stream_{sessionId}").SendAsync("FrameReceived", frame);
    }

    public async Task<object?> JoinAsViewer(string sessionId, string adminName)
    {
        var adminId = Context.UserIdentifier ?? Context.ConnectionId;

        try
        {
            var session = await _streamService.AddViewerAsync(sessionId, adminId, adminName, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"stream_{sessionId}");
            _connectionToSession[Context.ConnectionId] = sessionId;

            await Clients.Group($"stream_{sessionId}").SendAsync("ViewerJoined", new
            {
                adminId,
                adminName,
                viewerCount = session.Viewers.Count,
            });

            _logger.LogInformation("Admin {AdminName} joined stream {SessionId} as viewer", adminName, sessionId);

            return new
            {
                session.SessionId,
                session.PlayerId,
                session.TargetFps,
                session.JpegQuality,
                session.TotalFrames,
                session.Status,
            };
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("StreamError", ex.Message);
            return null;
        }
    }

    public async Task LeaveStream(string sessionId)
    {
        var adminId = Context.UserIdentifier ?? Context.ConnectionId;

        await _streamService.RemoveViewerAsync(sessionId, adminId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream_{sessionId}");
        _connectionToSession.TryRemove(Context.ConnectionId, out _);

        await Clients.Group($"stream_{sessionId}").SendAsync("ViewerLeft", new { adminId });
        _logger.LogInformation("Admin {AdminId} left stream {SessionId}", adminId, sessionId);
    }

    public async Task RequestFpsUpdate(string sessionId, double targetFps)
    {
        await _streamService.UpdateFpsAsync(sessionId, targetFps);

        var clamped = Math.Clamp(targetFps, 1.0, 30.0);
        await Clients.Group($"stream_{sessionId}").SendAsync("FpsUpdated", new
        {
            sessionId,
            targetFps = clamped,
        });
    }

    public async Task EndStream(string sessionId)
    {
        await _streamService.EndSessionAsync(sessionId);

        try
        {
            var dbSession = await _db.StreamSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (dbSession != null)
            {
                dbSession.Status = "ended";
                dbSession.EndedAt = DateTime.UtcNow;
                dbSession.EndedReason = "viewer-left";
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update stream session {SessionId} end status in DB", sessionId);
        }

        await Clients.Group($"stream_{sessionId}").SendAsync("StreamEnded", new { sessionId });
        _logger.LogInformation("Stream {SessionId} ended by client", sessionId);
    }

    public async Task GetActiveStreams()
    {
        var sessions = await _streamService.GetActiveSessionsAsync();
        await Clients.Caller.SendAsync("ActiveStreams", sessions);
    }
}
