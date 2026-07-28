using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IVerdictService
{
    Task<VerdictResult> EvaluateAsync(
        IReadOnlyList<DetectionEventDto> events,
        string? playerId = null,
        CancellationToken cancellationToken = default);

    Task<VerdictResult> GetLastVerdictAsync(string? playerId = null, CancellationToken cancellationToken = default);
}
