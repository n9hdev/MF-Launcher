using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

public class ScreenStreamService : IScreenStreamService, IDisposable
{
    private readonly ILogger<ScreenStreamService> _logger;
    private static readonly Dictionary<string, ScreenStreamSessionDto> _sessions = new();
    private static readonly object _sessionLock = new();
    private readonly ScreenStreamConfigDto _config;
    private readonly Timer _cleanupTimer;

    private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public ScreenStreamService(ILogger<ScreenStreamService> logger)
    {
        _logger = logger;
        _config = new ScreenStreamConfigDto();
        _cleanupTimer = new Timer(async _ => await CleanupStaleSessionsAsync(), null, CleanupInterval, CleanupInterval);
    }

    public Task<ScreenStreamSessionDto> CreateSessionAsync(string playerId, string? linkedDetectionId = null)
    {
        lock (_sessionLock)
        {
            var existing = _sessions.Values.FirstOrDefault(s =>
                s.PlayerId == playerId && s.Status == "active");

            if (existing != null)
            {
                _logger.LogInformation("Reusing active stream session {SessionId} for player {PlayerId}",
                    existing.SessionId, playerId);
                return Task.FromResult(existing);
            }

            var session = new ScreenStreamSessionDto
            {
                PlayerId = playerId,
                Status = "active",
                LinkedDetectionId = linkedDetectionId,
            };

            _sessions[session.SessionId] = session;
            _logger.LogInformation("Created stream session {SessionId} for player {PlayerId} (detection: {DetectionId})",
                session.SessionId, playerId, linkedDetectionId ?? "none");

            return Task.FromResult(session);
        }
    }

    public Task<ScreenStreamSessionDto?> GetSessionAsync(string sessionId)
    {
        lock (_sessionLock)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }
    }

    public Task<List<StreamSummaryDto>> GetActiveSessionsAsync()
    {
        lock (_sessionLock)
        {
            var summaries = new List<StreamSummaryDto>(_sessions.Count);
            foreach (var s in _sessions.Values)
            {
                if (s.Status == "active")
                    summaries.Add(MapToSummary(s));
            }
            return Task.FromResult(summaries);
        }
    }

    public Task<List<StreamSummaryDto>> GetHistoryAsync(string playerId, int limit = 20)
    {
        lock (_sessionLock)
        {
            var summaries = new List<StreamSummaryDto>(Math.Min(_sessions.Count, limit));
            foreach (var s in _sessions.Values)
            {
                if (s.PlayerId != playerId)
                    continue;
                summaries.Add(MapToSummary(s));
            }
            summaries.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
            if (summaries.Count > limit)
                summaries.RemoveRange(limit, summaries.Count - limit);
            return Task.FromResult(summaries);
        }
    }

    public Task<bool> EndSessionAsync(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Status = "ended";
                session.EndedAt = DateTime.UtcNow;
                session.DurationSeconds = (session.EndedAt.Value - session.StartedAt).TotalSeconds;
                _logger.LogInformation("Ended stream session {SessionId} for player {PlayerId} ({Frames} frames, {Duration:F1}s)",
                    sessionId, session.PlayerId, session.TotalFrames, session.DurationSeconds);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<ScreenStreamSessionDto> AddViewerAsync(string sessionId, string adminId, string adminName, string connectionId)
    {
        lock (_sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session {sessionId} not found");

            if (session.Viewers.Count >= _config.MaxViewers)
                throw new InvalidOperationException($"Session {sessionId} already has maximum viewers ({_config.MaxViewers})");

            var existing = session.Viewers.FirstOrDefault(v => v.AdminId == adminId);
            if (existing != null)
            {
                existing.ConnectionId = connectionId;
                existing.LeftAt = null;
                return Task.FromResult(session);
            }

            session.Viewers.Add(new StreamViewerDto
            {
                AdminId = adminId,
                AdminName = adminName,
                ConnectionId = connectionId,
                JoinedAt = DateTime.UtcNow,
            });

            _logger.LogInformation("Admin {AdminName} ({AdminId}) joined stream {SessionId}", adminName, adminId, sessionId);
            return Task.FromResult(session);
        }
    }

    public Task<bool> RemoveViewerAsync(string sessionId, string adminId)
    {
        lock (_sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return Task.FromResult(false);

            var viewer = session.Viewers.FirstOrDefault(v => v.AdminId == adminId);
            if (viewer != null)
            {
                viewer.LeftAt = DateTime.UtcNow;
                session.Viewers.Remove(viewer);
                _logger.LogInformation("Admin {AdminId} left stream {SessionId}", adminId, sessionId);

                if (session.Viewers.Count == 0 && session.Status == "active")
                {
                    _logger.LogInformation("Auto-ending stream {SessionId} — no viewers remaining", sessionId);
                    session.Status = "ended";
                    session.EndedAt = DateTime.UtcNow;
                    session.DurationSeconds = (session.EndedAt.Value - session.StartedAt).TotalSeconds;
                }

                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> UpdateFpsAsync(string sessionId, double targetFps)
    {
        lock (_sessionLock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                var clamped = Math.Clamp(targetFps, 1.0, 30.0);
                session.TargetFps = clamped;
                _logger.LogInformation("Stream {SessionId} FPS updated to {Fps}", sessionId, clamped);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<int> GetViewerCountAsync(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
                return Task.FromResult(session.Viewers.Count);
            return Task.FromResult(0);
        }
    }

    public Task<int> IncrementFrameCountAsync(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.TotalFrames++;
                return Task.FromResult(session.TotalFrames);
            }
            return Task.FromResult(0);
        }
    }

    public bool CanAcceptMoreViewers(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
                return session.Viewers.Count < _config.MaxViewers;
            return false;
        }
    }

    public Task CleanupStaleSessionsAsync()
    {
        lock (_sessionLock)
        {
            var now = DateTime.UtcNow;
            var staleKeys = new List<string>();
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.Status == "active" && (now - kvp.Value.StartedAt) > StaleTimeout)
                {
                    kvp.Value.Status = "timed_out";
                    kvp.Value.EndedAt = now;
                    kvp.Value.DurationSeconds = (now - kvp.Value.StartedAt).TotalSeconds;
                    _logger.LogWarning("Stream session {SessionId} for player {PlayerId} timed out after {Duration:F1}s",
                        kvp.Key, kvp.Value.PlayerId, kvp.Value.DurationSeconds);
                    staleKeys.Add(kvp.Key);
                }
                else if (kvp.Value.Status == "timed_out" || kvp.Value.Status == "ended")
                {
                    if ((now - (kvp.Value.EndedAt ?? kvp.Value.StartedAt)).TotalHours > 1)
                        staleKeys.Add(kvp.Key);
                }
            }
            foreach (var key in staleKeys)
                _sessions.Remove(key);
            if (staleKeys.Count > 0)
                _logger.LogInformation("Cleaned up {Count} stale stream sessions", staleKeys.Count);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    private static StreamSummaryDto MapToSummary(ScreenStreamSessionDto s) => new()
    {
        SessionId = s.SessionId,
        PlayerId = s.PlayerId,
        Status = s.Status,
        StartedAt = s.StartedAt,
        DurationSeconds = s.DurationSeconds,
        TotalFrames = s.TotalFrames,
        ViewerCount = s.Viewers.Count,
        LinkedDetectionId = s.LinkedDetectionId,
    };
}
