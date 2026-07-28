using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class TeamCymruService : ITeamCymruService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMtasaPathFinder _pathFinder;
    private readonly ILogger<TeamCymruService> _logger;
    private readonly TeamCymruSettings _settings;
    private readonly ConcurrentDictionary<string, TeamCymruResultEntity> _cache = new();

    public bool IsAvailable => _settings.Enabled;

    public TeamCymruService(
        IServiceScopeFactory scopeFactory,
        IMtasaPathFinder pathFinder,
        IOptions<TeamCymruSettings> settings,
        ILogger<TeamCymruService> logger)
    {
        _scopeFactory = scopeFactory;
        _pathFinder = pathFinder;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<TeamCymruResult> LookupHashAsync(string sha256, string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new TeamCymruResult
            {
                Sha256 = sha256,
                FilePath = filePath ?? "",
                FileName = !string.IsNullOrWhiteSpace(filePath) ? Path.GetFileName(filePath) : "",
                ScanResult = "disabled",
                Description = "Team Cymru MHR is not enabled in configuration",
            };
        }

        var cached = _cache.GetOrAdd(sha256, _ => null!);
        if (cached != null && (DateTime.UtcNow - cached.ScannedAt).TotalHours < 24)
        {
            return MapFromCache(cached, filePath);
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetService<AppDbContext>();
            if (db != null)
            {
                var dbCached = await db.TeamCymruResults
                    .FirstOrDefaultAsync(r => r.Sha256 == sha256, cancellationToken);

                if (dbCached != null)
                {
                    _cache.TryAdd(sha256, dbCached);
                    return MapFromCache(dbCached, filePath);
                }
            }
        }

        var result = await QueryMhrAsync(sha256, filePath, cancellationToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<AppDbContext>();
            if (db != null)
            {
                db.TeamCymruResults.Add(new TeamCymruResultEntity
                {
                    Sha256 = sha256,
                    DetectionCount = result.DetectionCount,
                    TotalEngines = result.TotalEngines,
                    DetectionRate = result.DetectionRate,
                    ScanResult = result.ScanResult,
                    ScannedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache Team Cymru result for {Sha256}", sha256);
        }

        return result;
    }

    public async Task<IReadOnlyList<TeamCymruResult>> ScanGameDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<TeamCymruResult>();

        if (!_settings.Enabled)
        {
            _logger.LogWarning("Team Cymru MHR is disabled — skipping scan");
            return results;
        }

        var gamePath = _pathFinder.FindPath();
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            _logger.LogWarning("Cannot scan: MTA path not found");
            return results;
        }

        var files = new List<string>();
        var searchDirectories = new List<string> { gamePath };
        try
        {
            searchDirectories.AddRange(Directory.GetDirectories(gamePath, "*", SearchOption.AllDirectories));
        }
        catch { /* ignore */ }

        foreach (var dir in searchDirectories)
        {
            try
            {
                files.AddRange(Directory.GetFiles(dir, "*.exe"));
                files.AddRange(Directory.GetFiles(dir, "*.dll"));
            }
            catch { /* ignore */ }
        }

        foreach (var file in files)
        {
            var sha256 = await ComputeSha256Async(file, cancellationToken);
            var result = await LookupHashAsync(sha256, file, cancellationToken);
            if (result.ScanResult != "disabled")
                results.Add(result);
        }

        _logger.LogInformation("Team Cymru MHR scan complete: {Total} files", results.Count);
        return results;
    }

    private async Task<TeamCymruResult> QueryMhrAsync(string sha256, string? filePath, CancellationToken cancellationToken)
    {
        var fileName = !string.IsNullOrWhiteSpace(filePath) ? Path.GetFileName(filePath) : "";
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(_settings.Host, _settings.Port);
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            if (await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken)) != connectTask)
            {
                return new TeamCymruResult
                {
                    Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
                    ScanResult = "timeout", Description = $"MHR connection timed out after {timeout.TotalSeconds}s",
                };
            }

            if (connectTask.IsFaulted)
            {
                return new TeamCymruResult
                {
                    Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
                    ScanResult = "error", Description = $"MHR connection failed: {connectTask.Exception?.InnerException?.Message}",
                };
            }

            using var stream = tcp.GetStream();
            stream.ReadTimeout = _settings.TimeoutSeconds * 1000;
            stream.WriteTimeout = _settings.TimeoutSeconds * 1000;

            // Send SHA256 query
            var query = Encoding.ASCII.GetBytes(sha256.ToLowerInvariant() + "\r\n");
            await stream.WriteAsync(query, cancellationToken);

            // Read response
            using var reader = new StreamReader(stream, Encoding.ASCII);
            var response = await reader.ReadLineAsync(cancellationToken) ?? "";

            return ParseResponse(response, sha256, filePath, fileName);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Team Cymru MHR query error for {Sha256}", sha256);
            return new TeamCymruResult
            {
                Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
                ScanResult = "error", Description = $"MHR query error: {ex.Message}",
            };
        }
    }

    private static TeamCymruResult ParseResponse(string response, string sha256, string? filePath, string fileName)
    {
        var parts = response.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2 || !int.TryParse(parts[0], out var detCount))
        {
            return new TeamCymruResult
            {
                Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
                ScanResult = "unknown", Description = $"Unexpected MHR response: {response}",
            };
        }

        if (parts.Length >= 2 && int.TryParse(parts[1], out var totalEngines) && totalEngines > 0)
        {
            return new TeamCymruResult
            {
                Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
                DetectionCount = detCount,
                TotalEngines = totalEngines,
                DetectionRate = totalEngines > 0 ? (double)detCount / totalEngines : 0,
                ScanResult = detCount > 0 ? "suspicious" : "clean",
                Severity = ClassifySeverity(detCount, totalEngines),
                Description = detCount > 0
                    ? $"Team Cymru: {detCount}/{totalEngines} AV engines detected threats"
                    : "Team Cymru: No AV detections",
                EvidenceType = detCount > 0 ? "TeamCymruDetection" : "TeamCymruClean",
            };
        }

        return new TeamCymruResult
        {
            Sha256 = sha256, FilePath = filePath ?? "", FileName = fileName,
            DetectionCount = 0, TotalEngines = 0,
            ScanResult = "clean", Description = "Team Cymru: Hash not found in MHR database",
        };
    }

    private static string ClassifySeverity(int detCount, int totalEngines)
    {
        if (totalEngines == 0) return "info";
        var rate = (double)detCount / totalEngines;
        if (rate >= 0.5) return "critical";
        if (rate >= 0.2) return "high";
        if (rate >= 0.05) return "medium";
        return "low";
    }

    private static TeamCymruResult MapFromCache(TeamCymruResultEntity cached, string? filePath)
    {
        var fileName = !string.IsNullOrWhiteSpace(filePath) ? Path.GetFileName(filePath) : "";
        return new TeamCymruResult
        {
            Sha256 = cached.Sha256,
            FilePath = filePath ?? "",
            FileName = fileName,
            DetectionCount = cached.DetectionCount,
            TotalEngines = cached.TotalEngines,
            DetectionRate = cached.DetectionRate,
            ScanResult = cached.ScanResult,
            Severity = ClassifySeverity(cached.DetectionCount, cached.TotalEngines),
            Description = cached.DetectionCount > 0
                ? $"Team Cymru: {cached.DetectionCount}/{cached.TotalEngines} AV engines detected threats (cached)"
                : "Team Cymru: No AV detections (cached)",
            EvidenceType = cached.DetectionCount > 0 ? "TeamCymruDetection" : "TeamCymruClean",
        };
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fs, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }
}
