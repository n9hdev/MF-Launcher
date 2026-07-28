using AntiCheat.Core.Models;

namespace AntiCheat.Core.Interfaces;

public interface IPeAnalysisService
{
    Task<PeAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct = default);
    Task<PeAnalysisResult> AnalyzeAsync(byte[] fileData, string? filePath = null, CancellationToken ct = default);
}
