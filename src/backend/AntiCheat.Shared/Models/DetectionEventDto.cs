namespace AntiCheat.Shared.Models;

public class DetectionEventDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? EvidencePath { get; set; }
    public string? PlayerId { get; set; }
    public string? ProcessName { get; set; }
}

public class ProtectionStatusDto
{
    public string MemoryScanner { get; set; } = "active";
    public string ProcessAnalyzer { get; set; } = "active";
    public string InjectionDetector { get; set; } = "active";
    public string KernelScanner { get; set; } = "active";
    public string YaraScanner { get; set; } = "active";
    public string AntiInjectionMonitor { get; set; } = "active";
    public string ModuleIntegrityScanner { get; set; } = "active";
    public string MemoryRegionAnalyzer { get; set; } = "active";
    public string InjectionTimingAnalyzer { get; set; } = "active";
    public string AntiTamperService { get; set; } = "active";
}

public class SystemHealthDto
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double NetworkLatency { get; set; }
    public DateTime LastScanTime { get; set; }
    public int ActiveModules { get; set; }
}
