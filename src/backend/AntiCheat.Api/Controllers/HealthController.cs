using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AntiCheat.Core.Data;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IServiceScopeFactory scopeFactory, ILogger<HealthController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbOk = await db.Database.CanConnectAsync();
            if (!dbOk)
                return Ok(new { healthy = false, serviceStatus = "degraded", message = "Database connection failed" });
            return Ok(new { healthy = true, serviceStatus = "running", message = "All systems operational" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return Ok(new { healthy = false, serviceStatus = "degraded", message = ex.Message });
        }
    }

    [HttpPost("restart")]
    public async Task<IActionResult> RestartService()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "stop AntiCheatApi",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart service");
            }
        });
        return Ok(new { message = "Service restart initiated. The page will reload automatically." });
    }
}
