using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;

namespace AntiCheat.Api.Hubs;

public class AntiCheatHub : Hub
{
    private readonly ILogger<AntiCheatHub> _logger;
    private readonly BanService _banService;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserConnectionTracker _connectionTracker;

    private static readonly string[] StaffRoles = { "moderator", "admin", "superadmin" };

    public AntiCheatHub(
        ILogger<AntiCheatHub> logger,
        BanService banService,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IUserConnectionTracker connectionTracker)
    {
        _logger = logger;
        _banService = banService;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _connectionTracker = connectionTracker;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await TryUpdateOnlineStatus(online: true);
        await Clients.Caller.SendAsync("StatusUpdate", new ProtectionStatusDto());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await TryUpdateOnlineStatus(online: false);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task TryUpdateOnlineStatus(bool online)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId) || !StaffRoles.Contains(role))
            return;

        try
        {
            if (online)
            {
                var count = _connectionTracker.AddConnection(userId);
                if (count == 1)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await db.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.Status = "online";
                        await db.SaveChangesAsync();
                        _logger.LogInformation("Staff {UserId} is now online", userId);
                    }
                }
            }
            else
            {
                var count = _connectionTracker.RemoveConnection(userId);
                if (count == 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await db.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.Status = "offline";
                        await db.SaveChangesAsync();
                        _logger.LogInformation("Staff {UserId} is now offline", userId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update online status for {UserId}", userId);
        }
    }

    public async Task JoinRoleGroup(string role)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, role);
        _logger.LogInformation("Client {Id} joined group: {Role}", Context.ConnectionId, role);
    }

    public async Task RequestScan()
    {
        await Clients.Caller.SendAsync("ScanResults", Array.Empty<object>());
    }

    public async Task RequestPreLaunchScan()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            var activeBan = await _banService.GetActiveBanAsync(userId);
            if (activeBan != null)
            {
                _logger.LogInformation("Pre-launch scan skipped — user {UserId} is already banned", userId);
                await Clients.Caller.SendAsync("PreLaunchResults", Array.Empty<object>());
                return;
            }
        }

        await Clients.Caller.SendAsync("PreLaunchStarted");

        string? hardwareId = null;
        if (!string.IsNullOrEmpty(userId))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            if (user != null)
                hardwareId = user.HardwareId;
        }

        if (hardwareId != null && _cache.TryGetValue($"prelaunch_{hardwareId}", out List<DetectionEventDto>? results) && results != null)
        {
            _logger.LogInformation("Pre-launch results found in cache for HWID {HwId}: {Count} detections", hardwareId, results.Count);
            await Clients.Caller.SendAsync("PreLaunchResults", results);
            if (results.Count == 0)
                await Clients.Caller.SendAsync("GameLaunchUnlocked");
        }
        else
        {
            _logger.LogInformation("No pre-launch results yet for user {UserId} (HWID: {HwId}) — sending pending state", userId, hardwareId);
            await Clients.Caller.SendAsync("PreLaunchPending");
        }
    }

    public async Task RequestStatus()
    {
        await Clients.Caller.SendAsync("StatusUpdate", new ProtectionStatusDto());
    }

    public async Task JoinUserGroup(string playerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{playerId}");
        _logger.LogInformation("Client {Id} joined user group: {PlayerId}", Context.ConnectionId, playerId);
    }

    public async Task LeaveUserGroup(string playerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{playerId}");
        _logger.LogInformation("Client {Id} left user group: {PlayerId}", Context.ConnectionId, playerId);
    }
}
