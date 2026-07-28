using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Services;

public class ConfidenceScorer : IConfidenceScorer
{
    public double CalculateConfidence(double rawConfidence, int detectionCount, int totalProcesses)
    {
        var score = rawConfidence;

        if (detectionCount > 1)
            score = Math.Min(1.0, score + (detectionCount - 1) * 0.05);

        if (totalProcesses > 0)
        {
            var prevalence = (double)detectionCount / totalProcesses;
            if (prevalence < 0.01)
                score = Math.Min(1.0, score + 0.1);
        }

        return Math.Round(Math.Clamp(score, 0, 1), 4);
    }

    public string ClassifySeverity(double confidence)
    {
        return confidence switch
        {
            >= 0.9 => "critical",
            >= 0.7 => "high",
            >= 0.4 => "medium",
            _ => "low"
        };
    }

    public double ApplyModifiers(double baseConfidence, DetectionContext context)
    {
        var modified = baseConfidence;

        if (context.PlayerId != null)
            modified += 0.02;

        return Math.Round(Math.Clamp(modified, 0, 1), 4);
    }
}
