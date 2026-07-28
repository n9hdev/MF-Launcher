using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IBehavioralMonitorService
{
    Task<List<EvidenceFact>> ScanAsync(CancellationToken ct = default);
}
