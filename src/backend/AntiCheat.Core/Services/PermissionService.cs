using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;
    private readonly IAuthService _auth;

    private static readonly Dictionary<string, List<string>> RolePermissions = new()
    {
        ["player"] = new()
        {
            "dashboard:view",
            "protection:view",
            "protection:start_scan",
            "launch:game",
            "history:view",
            "reports:submit",
            "reports:view_own",
            "settings:view",
            "settings:edit_general",
            "settings:edit_appearance",
        },
        ["moderator"] = new()
        {
            "dashboard:view",
            "reports:view_queue",
            "reports:investigate",
            "reports:resolve",
            "players:search",
            "players:view_details",
            "alerts:view",
            "alerts:acknowledge",
            "chat:mod",
            "bans:soft_ban",
            "settings:view",
            "settings:edit_general",
        },
        ["admin"] = new()
        {
            "dashboard:view",
            "reports:view_queue",
            "reports:investigate",
            "reports:resolve",
            "players:search",
            "players:view_details",
            "alerts:view",
            "alerts:acknowledge",
            "chat:mod",
            "bans:manage",
            "bans:permanent",
            "analytics:view",
            "appeals:view",
            "appeals:resolve",
            "whitelist:view",
            "whitelist:manage",
            "settings:view",
            "settings:edit_general",
            "settings:edit_security",
        },
        ["superadmin"] = new()
        {
            "dashboard:view",
            "protection:view",
            "protection:start_scan",
            "launch:game",
            "history:view",
            "reports:submit",
            "reports:view_own",
            "reports:view_queue",
            "reports:investigate",
            "reports:resolve",
            "players:search",
            "players:view_details",
            "alerts:view",
            "alerts:acknowledge",
            "chat:mod",
            "bans:manage",
            "bans:permanent",
            "bans:soft_ban",
            "analytics:view",
            "appeals:view",
            "appeals:resolve",
            "whitelist:view",
            "whitelist:manage",
            "command:execute",
            "telemetry:view",
            "detection:configure",
            "detection:view",
            "rules:manage",
            "infrastructure:view",
            "infrastructure:manage",
            "audit:view",
            "settings:view",
            "settings:edit_all",
        },
    };

    private static readonly List<FeatureFlagDto> FeatureFlags = new()
    {
        new() { Key = "detection.memory_scanner", Label = "Memory Scanner", Description = "Scan process memory for injections", Enabled = true },
        new() { Key = "detection.process_analyzer", Label = "Process Analyzer", Description = "Analyze running processes for suspicious behavior", Enabled = true },
        new() { Key = "detection.injection_detector", Label = "Injection Detector", Description = "Detect DLL/ code injection attempts", Enabled = true },
        new() { Key = "detection.kernel_scanner", Label = "Kernel Scanner", Description = "Scan kernel-mode drivers and structures", Enabled = true },
        new() { Key = "detection.yara_scanner", Label = "YARA Scanner", Description = "Pattern matching with YARA rules", Enabled = true },
        new() { Key = "detection.network_monitor", Label = "Network Monitor", Description = "Monitor network traffic for anomalies", Enabled = true },
        new() { Key = "experimental.real_time_protection", Label = "Real-Time Protection", Description = "Enable real-time threat blocking", Enabled = false },
        new() { Key = "experimental.ai_detection", Label = "AI Detection", Description = "Machine learning based anomaly detection", Enabled = false },
        new() { Key = "experimental.behavior_analysis", Label = "Behavior Analysis", Description = "Heuristic behavior analysis engine", Enabled = false },
        new() { Key = "maintenance_mode", Label = "Maintenance Mode", Description = "Put the system in maintenance mode", Enabled = false },
    };

    public PermissionService(ILogger<PermissionService> logger, IAuthService auth)
    {
        _logger = logger;
        _auth = auth;
    }

    public async Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var role = await GetUserRoleAsync(userId);
        var permissions = RolePermissions.GetValueOrDefault(role, new List<string>());
        _logger.LogDebug("User {UserId} role {Role} has {Count} permissions", userId, role, permissions.Count);
        return permissions;
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        var role = await GetUserRoleAsync(userId);
        var permissions = RolePermissions.GetValueOrDefault(role, new List<string>());
        return permissions.Contains(permission);
    }

    public async Task<UserPermissionsResponse> GetUserPermissionsResponseAsync(string userId, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        var role = await GetUserRoleAsync(userId);
        return new UserPermissionsResponse
        {
            Permissions = permissions,
            Role = role,
        };
    }

    public Task<List<FeatureFlagDto>> GetFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FeatureFlags.ToList());
    }

    public Task<bool> IsFeatureEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        var flag = FeatureFlags.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(flag?.Enabled ?? false);
    }

    public async Task<AllPermissionsResponse> GetAllPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allPermissions = RolePermissions.Values
            .SelectMany(p => p)
            .Distinct()
            .Select(name =>
            {
                var parts = name.Split(':');
                return new PermissionDto
                {
                    Name = name,
                    Description = $"Grants access to {parts.Last()} in {parts.First()}",
                    Category = parts.First(),
                };
            })
            .ToList();

        var userResponse = await GetUserPermissionsResponseAsync(userId, cancellationToken);

        return new AllPermissionsResponse
        {
            AllPermissions = allPermissions,
            User = userResponse,
        };
    }

    private async Task<string> GetUserRoleAsync(string userId)
    {
        try
        {
            var user = await _auth.GetUserByIdAsync(userId);
            return user.Role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get role for user {UserId}", userId);
            return "player";
        }
    }
}
