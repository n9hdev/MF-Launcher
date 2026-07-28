using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IScreenCaptureService
{
    Task<ScreenshotCaptureDto> CaptureAsync(ScreenshotRequest request, CancellationToken ct = default);
    Task<List<ScreenshotCaptureDto>> GetHistoryAsync(string playerId, int limit = 50);
    Task<ScreenshotCaptureDto?> GetByIdAsync(string id);
    Task<string?> StoreScreenshotAsync(ScreenshotCaptureDto screenshot);
    bool VerifyHmac(ScreenshotCaptureDto screenshot, string secret);
    string SignScreenshot(ScreenshotCaptureDto screenshot, string secret);
}
