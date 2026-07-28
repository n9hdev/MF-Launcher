using System.Security.Claims;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/modchat")]
[Authorize(Roles = "moderator,admin,superadmin")]
public class ModChatController : ControllerBase
{
    private readonly IModChatService _chat;

    public ModChatController(IModChatService chat)
    {
        _chat = chat;
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(CancellationToken ct)
    {
        var messages = await _chat.GetMessagesAsync();
        return Ok(messages);
    }

    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineModerators(CancellationToken ct)
    {
        var online = await _chat.GetOnlineModeratorsAsync();
        return Ok(online);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "moderator";

        var message = await _chat.SendMessageAsync(userId, username, request.Message, null, role);
        return Ok(message);
    }

    [HttpPost("send/attachment")]
    public async Task<IActionResult> SendAttachment(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "moderator";

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var imageData = ms.ToArray();

        var cloudinary = HttpContext.RequestServices.GetRequiredService<ICloudinaryService>();
        var url = await cloudinary.UploadScreenshotAsync(imageData, file.FileName, "modchat", ct);

        var message = await _chat.SendMessageAsync(userId, username, "", url, role);
        return Ok(message);
    }
}
