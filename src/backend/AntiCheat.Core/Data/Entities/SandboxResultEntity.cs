using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("SandboxResults")]
public class SandboxResultEntity
{
    [Key]
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Verdict { get; set; } = "unknown";

    public int ProcessCount { get; set; }
    public int FileWriteCount { get; set; }
    public int RegistryWriteCount { get; set; }
    public int NetworkConnectionCount { get; set; }
    public int MutexCount { get; set; }
    public int DllLoadCount { get; set; }
    public int ServiceCount { get; set; }

    public bool CreatedSuspiciousProcess { get; set; }
    public bool WroteExecutableFile { get; set; }
    public bool ConnectedToNetwork { get; set; }
    public bool ModifiedRegistry { get; set; }

    public double SuspicionScore { get; set; }

    [MaxLength(4000)]
    public string DetailsJson { get; set; } = "[]";

    public DateTime AnalysedAt { get; set; } = DateTime.UtcNow;
}
