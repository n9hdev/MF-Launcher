using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IConfidenceScorer
{
    double CalculateConfidence(double rawConfidence, int detectionCount, int totalProcesses);
    string ClassifySeverity(double confidence);
    double ApplyModifiers(double baseConfidence, DetectionContext context);
}
