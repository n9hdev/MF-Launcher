using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/game")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly IGameLauncher _game;

    public GameController(IGameLauncher game)
    {
        _game = game;
    }

    [HttpPost("launch")]
    public async Task<IActionResult> Launch([FromBody] GameLaunchRequest request, CancellationToken ct)
    {
        var success = await _game.LaunchGameAsync(request.GamePath, ct);
        return Ok(new { success });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        await _game.StopGameAsync();
        return Ok(new { message = "Game stopped" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await _game.GetStatusAsync();
        return Ok(status);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct)
    {
        var checks = await _game.GetLaunchChecksAsync();
        return Ok(checks);
    }

    [HttpGet("path")]
    public async Task<IActionResult> GetPath(CancellationToken ct)
    {
        var path = await _game.GetGamePathAsync();
        return Ok(new { path });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _game.GetSettingsAsync();
        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] GameSettingsDto settings, CancellationToken ct)
    {
        await _game.UpdateSettingsAsync(settings);
        return Ok(settings);
    }
}

public class GameLaunchRequest
{
    public string GamePath { get; set; } = @"C:\Program Files\MTA San Andreas 1.6\MTA.exe";
}
