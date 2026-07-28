using AntiCheat.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "superadmin")]
public class SuperAdminController : ControllerBase
{
    private readonly ISuperAdminService _svc;

    public SuperAdminController(ISuperAdminService svc)
    {
        _svc = svc;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        return Ok(await _svc.GetStatsAsync());
    }

    [HttpGet("infrastructure/nodes")]
    public async Task<IActionResult> GetInfrastructureNodes(CancellationToken ct)
    {
        return Ok(await _svc.GetInfrastructureNodesAsync());
    }

    [HttpGet("infrastructure/health")]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
    {
        return Ok(await _svc.GetSystemHealthAsync());
    }

    [HttpGet("telemetry/metrics")]
    public async Task<IActionResult> GetTelemetryMetrics(CancellationToken ct)
    {
        return Ok(await _svc.GetTelemetryMetricsAsync());
    }

    [HttpGet("telemetry/resources")]
    public async Task<IActionResult> GetSystemResources(CancellationToken ct)
    {
        return Ok(await _svc.GetSystemResourcesAsync());
    }

    [HttpGet("detection/stats")]
    public async Task<IActionResult> GetDetectionCenterStats(CancellationToken ct)
    {
        return Ok(await _svc.GetDetectionCenterStatsAsync());
    }

    [HttpGet("detection/modules")]
    public async Task<IActionResult> GetModuleStatuses(CancellationToken ct)
    {
        return Ok(await _svc.GetModuleStatusesAsync());
    }

    [HttpGet("detection/config")]
    public async Task<IActionResult> GetEngineConfig(CancellationToken ct)
    {
        return Ok(await _svc.GetEngineConfigAsync());
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        return Ok(await _svc.GetRulesAsync());
    }

    [HttpGet("infrastructure/servers")]
    public async Task<IActionResult> GetServers(CancellationToken ct)
    {
        return Ok(await _svc.GetServersAsync());
    }

    [HttpGet("infrastructure/server-stats")]
    public async Task<IActionResult> GetInfrastructureStats(CancellationToken ct)
    {
        return Ok(await _svc.GetInfrastructureStatsAsync());
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(CancellationToken ct)
    {
        return Ok(await _svc.GetAuditLogsAsync());
    }
}
