using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("CertificateReputation")]
public class CertificateReputationEntity
{
    [Key]
    [MaxLength(64)]
    public string Thumbprint { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Issuer { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SerialNumber { get; set; } = string.Empty;

    public DateTime? NotBefore { get; set; }
    public DateTime? NotAfter { get; set; }

    public bool IsRevoked { get; set; }
    public bool IsSelfSigned { get; set; }
    public bool IsChainTrusted { get; set; }

    [MaxLength(2000)]
    public string ChainStatus { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Verdict { get; set; } = "unknown";

    public double ReputationScore { get; set; }

    public int TotalFilesSeen { get; set; }
    public int FilesFlagged { get; set; }

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastVerified { get; set; } = DateTime.UtcNow;
}
