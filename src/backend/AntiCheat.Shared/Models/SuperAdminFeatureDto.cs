namespace AntiCheat.Shared.Models;

public class SuperAdminStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveSessions { get; set; }
    public double DetectionEngineUptime { get; set; }
    public double SystemLoad { get; set; }
    public string DataProcessed { get; set; } = "2.4 TB";
}

public class InfrastructureNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string Uptime { get; set; } = string.Empty;
    public string Load { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

public class InfrastructureHealthDto
{
    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int Disk { get; set; }
    public int Network { get; set; }
}

public class TelemetryMetricDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Trend { get; set; } = "up";
}

public class SystemResourceDto
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class ModuleStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class EngineConfigDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class DetectionCenterStatsDto
{
    public double DetectionRate { get; set; }
    public string EngineVersion { get; set; } = "6.0.0";
    public string Uptime { get; set; } = "30d 4h";
    public int ConfigVersion { get; set; }
}

public class RuleEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public bool Enabled { get; set; }
    public int Hits { get; set; }
    public string LastMatch { get; set; } = string.Empty;
}

public class ServerNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public int Cpu { get; set; }
    public int Mem { get; set; }
    public int Disk { get; set; }
    public string Region { get; set; } = string.Empty;
}

public class InfrastructureStatsDto
{
    public int TotalServers { get; set; }
    public int Online { get; set; }
    public double AvgCpu { get; set; }
    public double AvgMem { get; set; }
}

public class AuditLogEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
}
