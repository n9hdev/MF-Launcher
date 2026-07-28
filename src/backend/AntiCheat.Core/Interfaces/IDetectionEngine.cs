using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IDetectionEngine
{
    IReadOnlyList<IDetector> Detectors { get; }
    Task<IReadOnlyList<DetectionEventDto>> RunFullScanAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DetectionEventDto>> RunPreLaunchScanAsync(CancellationToken cancellationToken = default);
    Task RunContinuousScanAsync(CancellationToken ct);
    Task<ProtectionStatusDto> GetStatusAsync();
    Task EnableDetector(string name);
    Task DisableDetector(string name);
    event EventHandler<DetectionEventDto>? OnDetection;
    event EventHandler? OnPreLaunchComplete;
}
