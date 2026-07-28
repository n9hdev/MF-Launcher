using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IRiskEngine
{
    Task<RiskAssessmentDto> AssessRiskAsync(IReadOnlyList<DetectionEventDto> events, CancellationToken ct = default);
    double CalculatePlayerRiskScore(string playerId, IEnumerable<DetectionEventDto> history);
    string ClassifyRiskLevel(double score);
}
