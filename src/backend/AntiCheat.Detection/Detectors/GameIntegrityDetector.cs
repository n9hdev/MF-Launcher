using System.Security.Cryptography;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Detection.Detectors;

[DetectionPlugin("Game Integrity Detector", "1.0.0", "Hashes known MTA:SA game files and detects modifications against known-good baseline")]
public class GameIntegrityDetector : IDetector
{
    private readonly ILogger<GameIntegrityDetector> _logger;
    private readonly IMtasaPathFinder _pathFinder;
    private readonly IWhitelistProvider _whitelist;
    private readonly IGameHashVerifier? _hashVerifier;

    private static readonly string[] KnownGameFiles =
    {
        "gta_sa.exe",
        "Multi Theft Auto.exe",
        "multi-theft-auto.exe",
        "mta-client.exe",
        "mta-sa.exe",
        "core.dll",
        "net.dll",
        "game_sa.dll",
        "deathmatch.dll",
        "cdkey.dll",
        "xmll.dll",
        "pthread.dll",
    };

    public string Name => "Game Integrity Detector";
    public string Version => "1.0.0";
    public bool IsEnabled { get; set; } = true;

    public GameIntegrityDetector(
        ILogger<GameIntegrityDetector> logger,
        IMtasaPathFinder pathFinder,
        IWhitelistProvider whitelist,
        IGameHashVerifier? hashVerifier = null)
    {
        _logger = logger;
        _pathFinder = pathFinder;
        _whitelist = whitelist;
        _hashVerifier = hashVerifier;
    }

    public async Task<IReadOnlyList<DetectionEventDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            var gameDir = _pathFinder.FindPath();
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                _logger.LogDebug("MTA:SA installation not found, skipping game integrity check");
                return results;
            }

            var files = new List<GameFileHashEntry>();
            foreach (var fileName in KnownGameFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(gameDir, fileName);
                if (!File.Exists(fullPath))
                    continue;

                files.Add(new GameFileHashEntry
                {
                    FilePath = fullPath,
                    Sha256 = ComputeSha256(fullPath),
                    Md5 = ComputeMd5(fullPath),
                    FileSize = new FileInfo(fullPath).Length,
                });
            }

            if (_hashVerifier != null && files.Count > 0)
            {
                var verifyResponse = await _hashVerifier.VerifyHashesAsync(
                    new GameHashVerifyRequest { Files = files }, cancellationToken);

                if (verifyResponse?.Results != null)
                {
                    foreach (var result in verifyResponse.Results)
                    {
                        if (result.Status == "new")
                        {
                            _logger.LogInformation("Game integrity baseline set: {File} → {Hash}",
                                Path.GetFileName(result.FilePath), files.First(f => f.FilePath == result.FilePath).Sha256[..16]);
                        }
                        else if (result.Status == "mismatch")
                        {
                            var fileName = Path.GetFileName(result.FilePath);
                            var isWhitelisted = await _whitelist.IsProcessWhitelistedAsync(fileName, cancellationToken);
                            if (!isWhitelisted)
                            {
                                var currentEntry = files.First(f => f.FilePath == result.FilePath);
                                var expected = result.ExpectedSha256?[..16] ?? "unknown";
                                results.Add(new DetectionEventDto
                                {
                                    Type = "Game File Modified",
                                    Severity = "critical",
                                    Description = $"Game file '{fileName}' hash mismatch — expected {expected}..., got {currentEntry.Sha256[..16]}... (size: {currentEntry.FileSize})",
                                    Confidence = 0.95,
                                    Timestamp = DateTime.UtcNow,
                                    ProcessName = fileName,
                                });

                                _logger.LogWarning("Game file integrity violation: {File} expected={Expected} actual={Actual}",
                                    fileName, expected, currentEntry.Sha256[..16]);
                            }
                        }
                    }
                }
            }
            else
            {
                _logger.LogDebug("Game integrity verifier not available, skipping");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Game integrity scan failed");
        }

        return results;
    }

    public Task<DetectionResult> AnalyzeAsync(DetectionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DetectionResult { ThreatDetected = false });
    }

    private static string ComputeSha256(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ComputeMd5(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
