using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IActivityService
{
    Task<List<ActivityEventDto>> GetRecentActivityAsync(string userId, int count = 10, CancellationToken cancellationToken = default);
}
