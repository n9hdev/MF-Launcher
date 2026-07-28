namespace AntiCheat.Core.Interfaces;

public interface ICloudinaryService
{
    Task<string> UploadScreenshotAsync(byte[] imageData, string fileName, string? folder = null, CancellationToken ct = default);
    Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default);
}
