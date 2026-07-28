using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Service.Services;

public class GameHashVerifier : IGameHashVerifier
{
    private readonly ApiClientService _apiClient;

    public GameHashVerifier(ApiClientService apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<GameHashVerifyResponse?> VerifyHashesAsync(GameHashVerifyRequest request, CancellationToken ct = default)
    {
        return await _apiClient.VerifyGameHashesAsync(request, ct);
    }
}
