using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntiCheat.Core.Data.Entities;

[Table("GameFileHashes")]
public class GameFileHashEntity
{
    [Key]
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Md5 { get; set; }

    public long FileSize { get; set; }

    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    public DateTime LastVerified { get; set; } = DateTime.UtcNow;

    public int VerificationCount { get; set; } = 1;
}
