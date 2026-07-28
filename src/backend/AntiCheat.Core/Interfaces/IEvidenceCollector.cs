using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IEvidenceCollector
{
    Task<EvidenceRecordDto> CaptureProcessSnapshotAsync(int processId, DetectionEventDto ev, CancellationToken ct = default);
    Task<List<EvidenceRecordDto>> GetEvidenceAsync(string detectionEventId);
    Task<string?> SaveEvidenceToDiskAsync(EvidenceRecordDto evidence, string basePath);
}
