using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Core.Services;

public class SuperAdminService : ISuperAdminService
{
    private readonly AppDbContext _db;

    public SuperAdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SuperAdminStatsDto> GetStatsAsync()
    {
        return new SuperAdminStatsDto
        {
            TotalUsers = await _db.Users.CountAsync(),
            ActiveSessions = await _db.Sessions.CountAsync(s => s.IsActive),
            DetectionEngineUptime = 99.8,
            SystemLoad = 42.0,
            DataProcessed = "2.4 TB",
        };
    }

    public Task<List<InfrastructureNodeDto>> GetInfrastructureNodesAsync()
    {
        return Task.FromResult(new List<InfrastructureNodeDto>
        {
            new() { Name = "Detection Engine v6.0.0", Status = "active", Uptime = "14d 7h 32m", Load = "34%", Region = "US-East" },
            new() { Name = "API Server", Status = "active", Uptime = "30d 0h 12m", Load = "52%", Region = "US-East" },
            new() { Name = "Database Cluster", Status = "active", Uptime = "90d 4h 55m", Load = "28%", Region = "US-West" },
            new() { Name = "WebSocket Gateway", Status = "active", Uptime = "14d 7h 32m", Load = "18%", Region = "EU-West" },
            new() { Name = "Update Service", Status = "active", Uptime = "45d 2h 10m", Load = "5%", Region = "US-East" },
            new() { Name = "Backup Node", Status = "warning", Uptime = "7d 3h 45m", Load = "12%", Region = "AP-East" },
        });
    }

    public Task<InfrastructureHealthDto> GetSystemHealthAsync()
    {
        return Task.FromResult(new InfrastructureHealthDto { Cpu = 34, Memory = 62, Disk = 45, Network = 28 });
    }

    public Task<List<TelemetryMetricDto>> GetTelemetryMetricsAsync()
    {
        return Task.FromResult(new List<TelemetryMetricDto>
        {
            new() { Label = "Avg Response Time", Value = "24ms", Change = "+2ms", Trend = "up" },
            new() { Label = "P99 Latency", Value = "89ms", Change = "-5ms", Trend = "down" },
            new() { Label = "Throughput", Value = "2.4k req/s", Change = "+12%", Trend = "up" },
            new() { Label = "Error Rate", Value = "0.02%", Change = "-0.01%", Trend = "down" },
            new() { Label = "Active Connections", Value = "1,847", Change = "+234", Trend = "up" },
            new() { Label = "Cache Hit Rate", Value = "94.3%", Change = "+1.2%", Trend = "up" },
        });
    }

    public Task<List<SystemResourceDto>> GetSystemResourcesAsync()
    {
        return Task.FromResult(new List<SystemResourceDto>
        {
            new() { Label = "CPU Usage", Value = 34, Color = "bg-primary-500" },
            new() { Label = "Memory", Value = 62, Color = "bg-violet-500" },
            new() { Label = "Network Bandwidth", Value = 28, Color = "bg-emerald-500" },
            new() { Label = "Power Usage", Value = 45, Color = "bg-amber-500" },
        });
    }

    public Task<DetectionCenterStatsDto> GetDetectionCenterStatsAsync()
    {
        return Task.FromResult(new DetectionCenterStatsDto
        {
            DetectionRate = 99.75,
            EngineVersion = "6.0.0",
            Uptime = "30d 4h",
            ConfigVersion = 42,
        });
    }

    public Task<List<ModuleStatusDto>> GetModuleStatusesAsync()
    {
        return Task.FromResult(new List<ModuleStatusDto>
        {
            new() { Name = "memoryScanner", Status = "active" },
            new() { Name = "processAnalyzer", Status = "active" },
            new() { Name = "injectionDetector", Status = "active" },
            new() { Name = "kernelScanner", Status = "active" },
            new() { Name = "yaraScanner", Status = "active" },
            new() { Name = "networkMonitor", Status = "active" },
            new() { Name = "fileIntegrity", Status = "active" },
        });
    }

    public Task<List<EngineConfigDto>> GetEngineConfigAsync()
    {
        return Task.FromResult(new List<EngineConfigDto>
        {
            new() { Label = "Scan Interval", Value = "30s" },
            new() { Label = "Confidence Threshold", Value = "60%" },
            new() { Label = "Max Processes", Value = "500" },
            new() { Label = "Memory Limit", Value = "2 GB" },
            new() { Label = "Auto-Restart", Value = "Enabled" },
            new() { Label = "Log Level", Value = "Verbose" },
        });
    }

    public Task<List<RuleEntryDto>> GetRulesAsync()
    {
        return Task.FromResult(new List<RuleEntryDto>
        {
            new() { Id = "rl1", Name = "RWX Memory Detection", Type = "Memory", Severity = "Critical", Enabled = true, Hits = 145, LastMatch = "2m ago" },
            new() { Id = "rl2", Name = "DLL Injection Detection", Type = "Injection", Severity = "Critical", Enabled = true, Hits = 89, LastMatch = "15m ago" },
            new() { Id = "rl3", Name = "Unsigned Driver Check", Type = "Kernel", Severity = "High", Enabled = true, Hits = 34, LastMatch = "1h ago" },
            new() { Id = "rl4", Name = "Process Name Blacklist", Type = "Process", Severity = "Medium", Enabled = true, Hits = 234, LastMatch = "5m ago" },
            new() { Id = "rl5", Name = "Network Anomaly Detection", Type = "Network", Severity = "Medium", Enabled = false, Hits = 12, LastMatch = "2d ago" },
            new() { Id = "rl6", Name = "Certificate Chain Check", Type = "Certificate", Severity = "Low", Enabled = true, Hits = 567, LastMatch = "1m ago" },
            new() { Id = "rl7", Name = "Debugger Detection", Type = "Anti-Debug", Severity = "High", Enabled = true, Hits = 23, LastMatch = "30m ago" },
            new() { Id = "rl8", Name = "YARA Pattern Match", Type = "YARA", Severity = "Medium", Enabled = true, Hits = 67, LastMatch = "10m ago" },
        });
    }

    public Task<List<ServerNodeDto>> GetServersAsync()
    {
        return Task.FromResult(new List<ServerNodeDto>
        {
            new() { Name = "API-01", Type = "API Server", Status = "active", Cpu = 52, Mem = 61, Disk = 34, Region = "US-East" },
            new() { Name = "API-02", Type = "API Server", Status = "active", Cpu = 48, Mem = 57, Disk = 31, Region = "US-West" },
            new() { Name = "DB-Primary", Type = "Database", Status = "active", Cpu = 28, Mem = 72, Disk = 55, Region = "US-East" },
            new() { Name = "DB-Replica", Type = "Database", Status = "active", Cpu = 22, Mem = 45, Disk = 52, Region = "EU-West" },
            new() { Name = "WS-Gateway", Type = "WebSocket", Status = "active", Cpu = 18, Mem = 34, Disk = 12, Region = "EU-West" },
            new() { Name = "Cache-Node", Type = "Redis Cache", Status = "active", Cpu = 12, Mem = 88, Disk = 8, Region = "US-East" },
            new() { Name = "Update-Service", Type = "Update Server", Status = "active", Cpu = 5, Mem = 15, Disk = 42, Region = "US-East" },
            new() { Name = "Backup-01", Type = "Backup Server", Status = "warning", Cpu = 8, Mem = 22, Disk = 78, Region = "AP-East" },
        });
    }

    public Task<InfrastructureStatsDto> GetInfrastructureStatsAsync()
    {
        return Task.FromResult(new InfrastructureStatsDto { TotalServers = 8, Online = 8, AvgCpu = 24, AvgMem = 49 });
    }

    public async Task<List<AuditLogEntryDto>> GetAuditLogsAsync()
    {
        return await _db.AuditLogEntries
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditLogEntryDto
            {
                Id = a.Id,
                Action = a.Action,
                User = a.User,
                Target = a.Target,
                Details = a.Details,
                Timestamp = a.Timestamp,
                Ip = a.Ip,
            })
            .ToListAsync();
    }
}
