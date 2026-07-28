using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;
    private readonly CloudinarySettings _settings;

    public CloudinaryService(IOptions<CloudinarySettings> options, ILogger<CloudinaryService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        var account = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadScreenshotAsync(byte[] imageData, string fileName, string? folder = null, CancellationToken ct = default)
    {
        folder ??= _settings.Folder;
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, new MemoryStream(imageData)),
            Folder = folder,
            PublicId = Path.GetFileNameWithoutExtension(fileName),
            Overwrite = false,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
        {
            _logger.LogError("Cloudinary upload failed: {Error}", result.Error.Message);
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        _logger.LogInformation("Cloudinary upload success: {Url} (publicId: {PublicId})", result.SecureUrl, result.PublicId);
        return result.SecureUrl.ToString();
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default)
    {
        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);

        if (result.Error != null)
        {
            _logger.LogError("Cloudinary delete failed: {Error}", result.Error.Message);
            return false;
        }

        _logger.LogInformation("Cloudinary deleted: {PublicId}", publicId);
        return result.Result == "ok";
    }
}
