using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class ClamAvService : IClamAvService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMtasaPathFinder _pathFinder;
    private readonly ILogger<ClamAvService> _logger;
    private readonly ClamAvSettings _settings;
    private readonly ConcurrentDictionary<string, ClamAvResultEntity> _cache = new();
    private bool _disposed;

    public bool IsAvailable => _settings.Enabled;

    public ClamAvService(
        IServiceScopeFactory scopeFactory,
        IMtasaPathFinder pathFinder,
        IOptions<ClamAvSettings> settings,
        ILogger<ClamAvService> logger)
    {
        _scopeFactory = scopeFactory;
        _pathFinder = pathFinder;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<ClamAvResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new ClamAvResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                ScanResult = "disabled",
                Description = "ClamAV is not enabled in configuration",
            };
        }

        if (!File.Exists(filePath))
        {
            return new ClamAvResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                ScanResult = "error",
                Description = "File not found",
            };
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _settings.MaxFileSizeMb * 1024L * 1024L)
        {
            return new ClamAvResult
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                ScanResult = "skipped",
                Description = $"File exceeds max scan size ({_settings.MaxFileSizeMb}MB)",
            };
        }

        var sha256 = await ComputeSha256Async(filePath, cancellationToken);

        var cached = _cache.GetOrAdd(sha256, _ => null!);
        if (cached != null && (DateTime.UtcNow - cached.ScannedAt).TotalHours < 1)
        {
            return MapFromCache(cached, filePath);
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbCached = await db.ClamAvResults
                .FirstOrDefaultAsync(r => r.Sha256 == sha256, cancellationToken);

            if (dbCached != null)
            {
                _cache.TryAdd(sha256, dbCached);
                return MapFromCache(dbCached, filePath);
            }
        }

        var result = await ScanWithClamdAsync(filePath, sha256, cancellationToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ClamAvResults.Add(new ClamAvResultEntity
            {
                Sha256 = sha256,
                IsInfected = result.IsInfected,
                VirusName = result.VirusName,
                ScanResult = result.ScanResult,
                FileSize = fileInfo.Length,
                ScanDurationMs = result.ScanDurationMs,
                ScannedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache ClamAV result for {Sha256}", sha256);
        }

        return result;
    }

    public async Task<IReadOnlyList<ClamAvResult>> ScanGameDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ClamAvResult>();

        if (!_settings.Enabled)
        {
            _logger.LogWarning("ClamAV is disabled — skipping game directory scan");
            return results;
        }

        var gamePath = _pathFinder.FindPath();
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            _logger.LogWarning("Cannot scan: MTA path not found");
            return results;
        }

        var searchDirectories = new List<string> { gamePath };
        try
        {
            searchDirectories.AddRange(Directory.GetDirectories(gamePath, "*", SearchOption.AllDirectories));
        }
        catch { /* ignore inaccessible dirs */ }

        foreach (var dir in searchDirectories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.exe")
                    .Concat(Directory.GetFiles(dir, "*.dll")))
                {
                    var result = await ScanFileAsync(file, cancellationToken);
                    if (result.ScanResult != "skipped" && result.ScanResult != "disabled")
                        results.Add(result);
                }
            }
            catch { /* ignore inaccessible dirs */ }
        }

        _logger.LogInformation("ClamAV scan complete: {Total} files", results.Count);
        return results;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private async Task<ClamAvResult> ScanWithClamdAsync(string filePath, string sha256, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(_settings.Host, _settings.Port);
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            if (await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken)) != connectTask)
            {
                sw.Stop();
                return new ClamAvResult
                {
                    Sha256 = sha256,
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    ScanResult = "timeout",
                    Description = $"ClamAV connection timed out after {timeout.TotalSeconds}s",
                    ScanDurationMs = sw.Elapsed.TotalMilliseconds,
                    Severity = "info",
                };
            }

            if (connectTask.IsFaulted)
            {
                sw.Stop();
                return new ClamAvResult
                {
                    Sha256 = sha256,
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    ScanResult = "error",
                    Description = $"ClamAV connection failed: {connectTask.Exception?.InnerException?.Message}",
                    ScanDurationMs = sw.Elapsed.TotalMilliseconds,
                    Severity = "info",
                };
            }

            using var stream = tcp.GetStream();
            stream.ReadTimeout = _settings.TimeoutSeconds * 1000;
            stream.WriteTimeout = _settings.TimeoutSeconds * 1000;

            // Send INSTREAM command
            var command = "zINSTREAM\0"u8.ToArray();
            await stream.WriteAsync(command, cancellationToken);

            // Stream file data in chunks
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await fs.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var chunkLen = BitConverter.GetBytes(bytesRead);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(chunkLen);

                await stream.WriteAsync(chunkLen, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            // Send zero-length terminator
            var zero = new byte[] { 0, 0, 0, 0 };
            await stream.WriteAsync(zero, cancellationToken);

            // Read response
            using var reader = new StreamReader(stream);
            var response = await reader.ReadLineAsync(cancellationToken) ?? "";

            sw.Stop();

            var fileName = Path.GetFileName(filePath);
            if (response.StartsWith("stream:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = response[7..].Trim();
                if (rest.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return new ClamAvResult
                    {
                        Sha256 = sha256,
                        FilePath = filePath,
                        FileName = fileName,
                        IsInfected = false,
                        ScanResult = "clean",
                        Description = "No threats detected by ClamAV",
                        ScanDurationMs = sw.Elapsed.TotalMilliseconds,
                    };
                }

                return new ClamAvResult
                {
                    Sha256 = sha256,
                    FilePath = filePath,
                    FileName = fileName,
                    IsInfected = true,
                    VirusName = rest,
                    ScanResult = "infected",
                    Description = $"ClamAV detected: {rest}",
                    ScanDurationMs = sw.Elapsed.TotalMilliseconds,
                    Severity = "high",
                    EvidenceType = "ClamAvInfection",
                };
            }

            return new ClamAvResult
            {
                Sha256 = sha256,
                FilePath = filePath,
                FileName = fileName,
                ScanResult = "unknown",
                Description = $"Unexpected ClamAV response: {response}",
                ScanDurationMs = sw.Elapsed.TotalMilliseconds,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "ClamAV scan error for {File}", filePath);
            return new ClamAvResult
            {
                Sha256 = sha256,
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                ScanResult = "error",
                Description = $"ClamAV scan error: {ex.Message}",
                ScanDurationMs = sw.Elapsed.TotalMilliseconds,
            };
        }
    }

    private static ClamAvResult MapFromCache(ClamAvResultEntity cached, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (cached.IsInfected)
        {
            return new ClamAvResult
            {
                Sha256 = cached.Sha256,
                FilePath = filePath,
                FileName = fileName,
                IsInfected = true,
                VirusName = cached.VirusName,
                ScanResult = "infected",
                Description = $"ClamAV detected: {cached.VirusName}",
                Severity = "high",
                EvidenceType = "ClamAvInfection",
            };
        }

        return new ClamAvResult
        {
            Sha256 = cached.Sha256,
            FilePath = filePath,
            FileName = fileName,
            IsInfected = false,
            ScanResult = "clean",
            Description = "No threats detected by ClamAV (cached)",
            ScanDurationMs = cached.ScanDurationMs,
        };
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fs, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }
}
