using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IGameHashVerifier
{
    Task<GameHashVerifyResponse?> VerifyHashesAsync(GameHashVerifyRequest request, CancellationToken ct = default);
}
