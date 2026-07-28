using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IModChatService
{
    Task<List<ModChatMessageDto>> GetMessagesAsync();
    Task<List<ModeratorOnlineDto>> GetOnlineModeratorsAsync();
    Task<ModChatMessageDto> SendMessageAsync(string userId, string username, string message, string? attachmentUrl = null, string role = "moderator");
}
