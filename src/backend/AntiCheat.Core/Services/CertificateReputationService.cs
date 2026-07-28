using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class CertificateReputationService : ICertificateReputationService
{
    private readonly AppDbContext? _db;
    private readonly IMemoryCache? _cache;
    private readonly bool _useDb;
    private readonly IMtasaPathFinder _pathFinder;
    private readonly ILogger<CertificateReputationService> _logger;

    public CertificateReputationService(
        AppDbContext db,
        IMtasaPathFinder pathFinder,
        ILogger<CertificateReputationService> logger)
    {
        _db = db;
        _useDb = true;
        _pathFinder = pathFinder;
        _logger = logger;
    }

    public CertificateReputationService(
        IMemoryCache cache,
        IMtasaPathFinder pathFinder,
        ILogger<CertificateReputationService> logger)
    {
        _cache = cache;
        _useDb = false;
        _pathFinder = pathFinder;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CertificateEvidence>> ScanGameFilesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CertificateEvidence>();
        var gamePath = _pathFinder.FindPath();

        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            _logger.LogWarning("Cannot scan certificates: MTA path not found");
            return results;
        }

        var searchDirectories = new List<string> { gamePath };
        try
        {
            var subDirs = Directory.GetDirectories(gamePath, "*", SearchOption.AllDirectories);
            searchDirectories.AddRange(subDirs);
        }
        catch { /* ignore inaccessible dirs */ }

        var processed = 0;
        foreach (var dir in searchDirectories)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.exe")
                    .Concat(Directory.GetFiles(dir, "*.dll")))
                {
                    var evidence = await VerifyFileCertificateAsync(file, cancellationToken);
                    if (evidence != null)
                        results.Add(evidence);
                    processed++;
                }
            }
            catch { /* ignore inaccessible dirs */ }
        }

        _logger.LogInformation("Certificate reputation scan: {Total} files processed, {Results} results", processed, results.Count);
        return results;
    }

    public async Task<CertificateEvidence?> VerifyFileCertificateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            if (!IsPESigned(filePath))
            {
                // Files on disk being unsigned is NOT a threat indicator — many legitimate
                // applications (dev tools, game clients, installers, the anti-cheat itself)
                // are unsigned. The BehavioralDetector's UnsignedModuleInGame check handles
                // the real threat: unsigned DLLs INJECTED into the running game process.
                return new CertificateEvidence
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    IsSigned = false,
                    IsChainTrusted = false,
                    Severity = "info",
                    EvidenceType = "UnsignedModule",
                    Description = $"File is not digitally signed: {Path.GetFileName(filePath)}",
                };
            }

            using var cert = X509Certificate.CreateFromSignedFile(filePath);
            using var cert2 = new X509Certificate2(cert.Handle);
            var thumbprint = cert2.Thumbprint ?? ComputeSha1(cert2.GetRawCertData());

            if (_useDb)
            {
                var cached = await _db!.CertificateReputations
                    .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint, cancellationToken);

                if (cached != null && (DateTime.UtcNow - cached.LastVerified).TotalHours < 24)
                {
                    return MapFromCache(cached, filePath);
                }
            }
            else
            {
                var cacheKey = $"cert_{thumbprint}";
                if (_cache!.TryGetValue<CachedCertData>(cacheKey, out var cached) &&
                    cached != null &&
                    (DateTime.UtcNow - cached.LastVerified).TotalHours < 24)
                {
                    return MapFromCachedData(cached, filePath);
                }
            }

            var evidence = await VerifyChainAsync(cert2, filePath, thumbprint, cancellationToken);

            await UpdateCacheAsync(thumbprint, cert2, evidence, cancellationToken);

            return evidence;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Certificate verification failed for {File}", filePath);
            return null;
        }
    }

    private async Task<CertificateEvidence> VerifyChainAsync(
        X509Certificate2 certificate, string filePath, string thumbprint,
        CancellationToken cancellationToken)
    {
        var evidence = new CertificateEvidence
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Subject = certificate.Subject ?? "",
            Issuer = certificate.Issuer ?? "",
            Thumbprint = thumbprint,
            SerialNumber = certificate.SerialNumber ?? "",
            IsSigned = true,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
        };

        evidence.IsExpired = DateTime.UtcNow > evidence.NotAfter;
        evidence.IsSelfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase);

        try
        {
            using var chain = X509Chain.Create();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);

            var chainBuilt = await Task.Run(() => chain.Build(certificate), cancellationToken);

            evidence.IsChainTrusted = chainBuilt;

            foreach (var status in chain.ChainStatus)
            {
                evidence.ChainErrors.Add($"{status.Status}: {status.StatusInformation?.Trim()}");
            }

            evidence.IsRevoked = chain.ChainStatus.Any(s =>
                s.Status == X509ChainStatusFlags.Revoked ||
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "X509Chain verification error for {File}", filePath);
            evidence.ChainErrors.Add($"VerificationError: {ex.Message}");
        }

        evidence.Severity = ClassifySeverity(evidence);
        evidence.Description = BuildDescription(evidence);
        return evidence;
    }

    private async Task UpdateCacheAsync(string thumbprint, X509Certificate2 cert, CertificateEvidence evidence, CancellationToken ct)
    {
        try
        {
            if (_useDb)
            {
                var existing = await _db!.CertificateReputations
                    .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint, ct);

                if (existing != null)
                {
                    existing.IsRevoked = evidence.IsRevoked;
                    existing.IsChainTrusted = evidence.IsChainTrusted;
                    existing.ChainStatus = string.Join("; ", evidence.ChainErrors);
                    existing.LastVerified = DateTime.UtcNow;
                    existing.TotalFilesSeen++;
                    existing.ReputationScore = ComputeReputationScore(evidence);
                    existing.Verdict = evidence.Severity switch
                    {
                        "critical" or "high" => "suspicious",
                        _ => "trusted",
                    };
                }
                else
                {
                    _db.CertificateReputations.Add(new CertificateReputationEntity
                    {
                        Thumbprint = thumbprint,
                        Subject = evidence.Subject,
                        Issuer = evidence.Issuer,
                        SerialNumber = evidence.SerialNumber,
                        NotBefore = evidence.NotBefore,
                        NotAfter = evidence.NotAfter,
                        IsRevoked = evidence.IsRevoked,
                        IsSelfSigned = evidence.IsSelfSigned,
                        IsChainTrusted = evidence.IsChainTrusted,
                        ChainStatus = string.Join("; ", evidence.ChainErrors),
                        TotalFilesSeen = 1,
                        ReputationScore = ComputeReputationScore(evidence),
                        Verdict = evidence.Severity switch
                        {
                            "critical" or "high" => "suspicious",
                            _ => "trusted",
                        },
                        FirstSeen = DateTime.UtcNow,
                        LastVerified = DateTime.UtcNow,
                    });
                }

                await _db.SaveChangesAsync(ct);
            }
            else
            {
                var cacheKey = $"cert_{thumbprint}";
                var data = new CachedCertData
                {
                    Subject = evidence.Subject,
                    Issuer = evidence.Issuer,
                    Thumbprint = thumbprint,
                    SerialNumber = evidence.SerialNumber,
                    IsRevoked = evidence.IsRevoked,
                    IsSelfSigned = evidence.IsSelfSigned,
                    IsChainTrusted = evidence.IsChainTrusted,
                    ChainStatus = string.Join("; ", evidence.ChainErrors),
                    LastVerified = DateTime.UtcNow,
                };
                _cache!.Set(cacheKey, data, TimeSpan.FromHours(24));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache certificate reputation for {Thumbprint}", thumbprint);
        }
    }

    private static CertificateEvidence? MapFromCache(CertificateReputationEntity cached, string filePath)
    {
        return new CertificateEvidence
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Subject = cached.Subject,
            Issuer = cached.Issuer,
            Thumbprint = cached.Thumbprint,
            SerialNumber = cached.SerialNumber,
            IsSigned = true,
            IsRevoked = cached.IsRevoked,
            IsSelfSigned = cached.IsSelfSigned,
            IsChainTrusted = cached.IsChainTrusted,
            NotBefore = cached.NotBefore,
            NotAfter = cached.NotAfter,
            ChainErrors = string.IsNullOrWhiteSpace(cached.ChainStatus)
                ? new()
                : cached.ChainStatus.Split("; ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            Severity = !cached.IsChainTrusted
                ? (cached.ChainStatus.Contains("NotTimeValid") && !cached.ChainStatus.Contains(";") ? "info" : "high")
                : cached.IsSelfSigned ? "medium" : "info",
            EvidenceType = "CertificateReputation",
            Description = cached.IsChainTrusted
                ? $"Certificate trusted: {cached.Subject} (issued by {cached.Issuer})"
                : $"Certificate issues: {cached.ChainStatus}",
        };
    }

    private static string ClassifySeverity(CertificateEvidence e)
    {
        if (e.IsRevoked) return "critical";
        if (!e.IsChainTrusted)
        {
            // Only expired (NotTimeValid) without other errors → info (normal for old game files)
            var hasOnlyTimeError = e.ChainErrors.Count > 0
                && e.ChainErrors.All(err => err.StartsWith("NotTimeValid", StringComparison.OrdinalIgnoreCase));
            if (hasOnlyTimeError)
                return "info";
            return "high";
        }
        if (e.IsSelfSigned) return "medium";
        return "info";
    }

    private static string BuildDescription(CertificateEvidence e)
    {
        var parts = new List<string>();
        parts.Add($"Subject: {e.Subject}");

        if (e.IsRevoked) parts.Add("CERTIFICATE IS REVOKED");
        if (e.IsExpired) parts.Add($"Expired: {e.NotAfter:yyyy-MM-dd}");
        if (!e.IsChainTrusted) parts.Add($"Chain errors: {string.Join(", ", e.ChainErrors)}");
        if (e.IsSelfSigned) parts.Add("Self-signed certificate");

        parts.Add($"Issuer: {e.Issuer}");
        return string.Join(" | ", parts);
    }

    private static double ComputeReputationScore(CertificateEvidence e)
    {
        double score = 50;
        if (e.IsRevoked) score -= 40;
        if (e.IsExpired) score -= 15;
        if (!e.IsChainTrusted) score -= 20;
        if (e.IsSelfSigned) score -= 10;
        if (e.ChainErrors.Count == 0 && e.IsChainTrusted) score += 30;
        return Math.Clamp(score, 0, 100);
    }

    private static bool IsPESigned(string filePath)
    {
        try
        {
            using var cert = X509Certificate.CreateFromSignedFile(filePath);
            return cert != null;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha1(byte[] data)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }

    private static CertificateEvidence MapFromCachedData(CachedCertData cached, string filePath)
    {
        return new CertificateEvidence
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Subject = cached.Subject,
            Issuer = cached.Issuer,
            Thumbprint = cached.Thumbprint,
            SerialNumber = cached.SerialNumber,
            IsSigned = true,
            IsRevoked = cached.IsRevoked,
            IsSelfSigned = cached.IsSelfSigned,
            IsChainTrusted = cached.IsChainTrusted,
            ChainErrors = string.IsNullOrWhiteSpace(cached.ChainStatus)
                ? new()
                : cached.ChainStatus.Split("; ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            Severity = !cached.IsChainTrusted
                ? (cached.ChainStatus.Contains("NotTimeValid") && !cached.ChainStatus.Contains(";") ? "info" : "high")
                : cached.IsSelfSigned ? "medium" : "info",
            EvidenceType = "CertificateReputation",
            Description = cached.IsChainTrusted
                ? $"Certificate trusted: {cached.Subject} (issued by {cached.Issuer})"
                : $"Certificate issues: {cached.ChainStatus}",
        };
    }

    private class CachedCertData
    {
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public bool IsRevoked { get; set; }
        public bool IsSelfSigned { get; set; }
        public bool IsChainTrusted { get; set; }
        public string ChainStatus { get; set; } = string.Empty;
        public DateTime LastVerified { get; set; }
    }
}
