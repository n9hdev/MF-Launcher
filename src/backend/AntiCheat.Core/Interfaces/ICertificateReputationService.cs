using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public class CertificateEvidence
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsSelfSigned { get; set; }
    public bool IsExpired { get; set; }
    public bool IsChainTrusted { get; set; }
    public DateTime? NotBefore { get; set; }
    public DateTime? NotAfter { get; set; }
    public List<string> ChainErrors { get; set; } = new();
    public string Severity { get; set; } = "info";
    public string Description { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "CertificateReputation";
}

public interface ICertificateReputationService
{
    Task<IReadOnlyList<CertificateEvidence>> ScanGameFilesAsync(CancellationToken cancellationToken = default);
    Task<CertificateEvidence?> VerifyFileCertificateAsync(string filePath, CancellationToken cancellationToken = default);
}
