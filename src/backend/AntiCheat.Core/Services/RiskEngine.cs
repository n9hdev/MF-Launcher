using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Services;

public class RiskEngine : IRiskEngine
{
    public Task<RiskAssessmentDto> AssessRiskAsync(IReadOnlyList<DetectionEventDto> events, CancellationToken ct = default)
    {
        var activeEvents = events.Where(e => e.Severity != "low").ToList();
        var contributions = new Dictionary<string, double>();

        double totalScore = 0;
        foreach (var ev in activeEvents)
        {
            var severityWeight = ev.Severity switch
            {
                "critical" => 1.0,
                "high" => 0.7,
                "medium" => 0.4,
                _ => 0.1
            };

            var eventScore = severityWeight * ev.Confidence;
            totalScore += eventScore;

            var detectorKey = ev.Type.Split(':').First().Trim();
            contributions[detectorKey] = Math.Round(eventScore, 4);
        }

        var maxScore = Math.Min(activeEvents.Count * 1.0, 5.0);
        var normalizedScore = maxScore > 0 ? Math.Round(Math.Min(totalScore / maxScore, 1.0), 4) : 0;

        return Task.FromResult(new RiskAssessmentDto
        {
            OverallRiskScore = normalizedScore,
            RiskLevel = ClassifyRiskLevel(normalizedScore),
            ActiveThreats = activeEvents.ToList(),
            DetectorContributions = contributions,
            AssessedAt = DateTime.UtcNow,
        });
    }

    public double CalculatePlayerRiskScore(string playerId, IEnumerable<DetectionEventDto> history)
    {
        var recent = history.Where(e => e.Timestamp > DateTime.UtcNow.AddDays(-30)).ToList();
        if (recent.Count == 0) return 0;

        var score = recent.Sum(e => e.Confidence * (e.Severity switch
        {
            "critical" => 1.0,
            "high" => 0.7,
            "medium" => 0.4,
            _ => 0.1
        }));

        return Math.Round(Math.Min(score / Math.Max(recent.Count, 1), 1.0), 4);
    }

    public string ClassifyRiskLevel(double score)
    {
        return score switch
        {
            >= 0.8 => "critical",
            >= 0.5 => "high",
            >= 0.2 => "medium",
            _ => "low"
        };
    }
}
