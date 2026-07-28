using System.Diagnostics;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class EvidenceCollector : IEvidenceCollector
{
    private readonly ILogger<EvidenceCollector> _logger;
    private static readonly List<EvidenceRecordDto> _evidenceStore = new();
    private static readonly object _storeLock = new();
    private const int MaxEvidenceStoreSize = 10000;
    private const int TrimBatchSize = 1000;

    public EvidenceCollector(ILogger<EvidenceCollector> logger)
    {
        _logger = logger;
    }

    public Task<EvidenceRecordDto> CaptureProcessSnapshotAsync(int processId, DetectionEventDto ev, CancellationToken ct = default)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var snapshot = new EvidenceRecordDto
            {
                DetectionEventId = ev.Id,
                EvidenceType = "ProcessSnapshot",
                Data = $"Process: {process.ProcessName} (PID: {process.Id})\n" +
                       $"StartTime: {process.StartTime:O}\n" +
                       $"Memory: {process.WorkingSet64 / 1024 / 1024} MB\n" +
                       $"Modules: {process.Modules.Count}\n" +
                       $"Threads: {process.Threads.Count}",
                CollectedAt = DateTime.UtcNow,
            };

            lock (_storeLock)
            {
                if (_evidenceStore.Count >= MaxEvidenceStoreSize)
                    _evidenceStore.RemoveRange(0, TrimBatchSize);
                _evidenceStore.Add(snapshot);
            }
            _logger.LogInformation("Evidence captured for detection {EventId}: {Type}", ev.Id, snapshot.EvidenceType);
            return Task.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture process snapshot for PID {ProcessId}", processId);
            return Task.FromResult(new EvidenceRecordDto
            {
                DetectionEventId = ev.Id,
                EvidenceType = "ProcessSnapshot",
                Data = $"Failed to capture: {ex.Message}",
                CollectedAt = DateTime.UtcNow,
            });
        }
    }

    public Task<List<EvidenceRecordDto>> GetEvidenceAsync(string detectionEventId)
    {
        List<EvidenceRecordDto> results;
        lock (_storeLock)
        {
            results = _evidenceStore.Where(e => e.DetectionEventId == detectionEventId).ToList();
        }
        return Task.FromResult(results);
    }

    public async Task<string?> SaveEvidenceToDiskAsync(EvidenceRecordDto evidence, string basePath)
    {
        try
        {
            var dir = Path.Combine(basePath, "evidence", evidence.DetectionEventId);
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"{evidence.Id}.txt");
            await File.WriteAllTextAsync(filePath, evidence.Data);
            _logger.LogInformation("Evidence saved to {Path}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save evidence to disk");
            return null;
        }
    }
}
