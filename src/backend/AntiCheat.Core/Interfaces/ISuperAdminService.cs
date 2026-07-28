using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface ISuperAdminService
{
    Task<SuperAdminStatsDto> GetStatsAsync();
    Task<List<InfrastructureNodeDto>> GetInfrastructureNodesAsync();
    Task<InfrastructureHealthDto> GetSystemHealthAsync();
    Task<List<TelemetryMetricDto>> GetTelemetryMetricsAsync();
    Task<List<SystemResourceDto>> GetSystemResourcesAsync();
    Task<DetectionCenterStatsDto> GetDetectionCenterStatsAsync();
    Task<List<ModuleStatusDto>> GetModuleStatusesAsync();
    Task<List<EngineConfigDto>> GetEngineConfigAsync();
    Task<List<RuleEntryDto>> GetRulesAsync();
    Task<List<ServerNodeDto>> GetServersAsync();
    Task<InfrastructureStatsDto> GetInfrastructureStatsAsync();
    Task<List<AuditLogEntryDto>> GetAuditLogsAsync();
}
