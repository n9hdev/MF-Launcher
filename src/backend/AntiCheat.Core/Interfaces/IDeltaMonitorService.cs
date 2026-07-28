using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IDeltaMonitorService
{
    Task<IReadOnlyList<EvidenceFact>> ScanDeltaAsync(BaselineSnapshot baseline, CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceFact>> CheckCodeIntegrityAsync(BaselineSnapshot baseline, CancellationToken ct = default);
}
