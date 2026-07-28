using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IReputationService
{
    Task<ReputationLookupResponse> LookupAsync(ReputationLookupRequest request, CancellationToken ct = default);
    Task<ReputationLookupResponse> ReportAsync(ReputationReportRequest request, CancellationToken ct = default);
    Task<ReputationLookupResponse> SetVerdictAsync(ReputationVerdictRequest request, CancellationToken ct = default);
    Task<int> GetTotalEntriesAsync(CancellationToken ct = default);
}
