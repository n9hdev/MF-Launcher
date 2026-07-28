namespace AntiCheat.Core.Interfaces;

public class SandboxDetail
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}

public class SandboxResult
{
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Verdict { get; set; } = "unknown";
    public double SuspicionScore { get; set; }
    public string Severity { get; set; } = "info";
    public string Description { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "SandboxDetonation";
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
    public List<SandboxDetail> Details { get; set; } = new();
}

public interface ISandboxService
{
    bool IsAvailable { get; }
    Task<SandboxResult> AnalyseFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<SandboxResult?> GetCachedResultAsync(string sha256, CancellationToken cancellationToken = default);
}
