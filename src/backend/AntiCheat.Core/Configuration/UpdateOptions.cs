namespace AntiCheat.Core.Configuration;

public class UpdateOptions
{
    public string LatestVersion { get; set; } = "6.2.9";
    public string ReleaseDate { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string FallbackDownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public bool IsCritical { get; set; }
    public string MinSupportedVersion { get; set; } = "6.0.0";
    public string Changelog { get; set; } = "";
    public string Signature { get; set; } = "";
}
