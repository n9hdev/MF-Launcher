using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IDedupService
{
    Task<IReadOnlyList<DetectionEventDto>> DeduplicateAsync(
        IReadOnlyList<DetectionEventDto> events,
        CancellationToken cancellationToken = default);

    Task<bool> IsDuplicateAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default);

    Task MarkSeenAsync(
        string fingerprintHash,
        CancellationToken cancellationToken = default);

    Task<int> CleanupStaleEntriesAsync(CancellationToken cancellationToken = default);
}
