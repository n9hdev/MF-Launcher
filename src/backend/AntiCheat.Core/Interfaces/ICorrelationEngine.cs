using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface ICorrelationEngine
{
    double CurrentCorrelationScore { get; }
    double GetPlayerScore(string playerId);
    CorrelationResultDto Evaluate(IReadOnlyList<DetectionEventDto> events, string? playerId = null);
    string ClassifyCorrelation(double score);
}
