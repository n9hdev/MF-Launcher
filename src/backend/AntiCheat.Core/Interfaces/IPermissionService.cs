using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IPermissionService
{
    Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
    Task<UserPermissionsResponse> GetUserPermissionsResponseAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<FeatureFlagDto>> GetFeatureFlagsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsFeatureEnabledAsync(string key, CancellationToken cancellationToken = default);
    Task<AllPermissionsResponse> GetAllPermissionsAsync(string userId, CancellationToken cancellationToken = default);
}
