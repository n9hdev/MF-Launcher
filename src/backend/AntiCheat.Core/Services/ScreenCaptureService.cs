using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<ScreenCaptureService> _logger;
    private readonly IDesktopCaptureService _desktopCapture;
    private readonly ICloudinaryService _cloudinary;
    private static readonly List<ScreenshotCaptureDto> _captureStore = new();
    private static readonly object _storeLock = new();
    private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    public ScreenCaptureService(
        ILogger<ScreenCaptureService> logger,
        IDesktopCaptureService desktopCapture,
        ICloudinaryService cloudinary)
    {
        _logger = logger;
        _desktopCapture = desktopCapture;
        _cloudinary = cloudinary;
    }

    public async Task<ScreenshotCaptureDto> CaptureAsync(ScreenshotRequest request, CancellationToken ct = default)
    {
        var capture = new ScreenshotCaptureDto
        {
            PlayerId = request.PlayerId,
            DetectionEventId = request.DetectionEventId,
            CapturedAt = DateTime.UtcNow,
            Format = "jpeg",
        };

        try
        {
            // Capture real screenshot using DesktopCaptureService
            var localPath = _desktopCapture.CaptureScreenshot(request.Reason ?? "detection");
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                _logger.LogWarning("Desktop capture returned no file, using fallback");
                var fallback = GenerateDummyScreenshot(capture.Id, request.Reason ?? "manual");
                capture.ImageData = fallback;
            }
            else
            {
                capture.ImageData = await File.ReadAllBytesAsync(localPath, ct);
                capture.StoragePath = localPath;
            }

            // Upload to Cloudinary
            if (capture.ImageData.Length > 0)
            {
                try
                {
                    var cloudUrl = await _cloudinary.UploadScreenshotAsync(
                        capture.ImageData,
                        $"{capture.Id}.jpg",
                        $"screenshots/{request.PlayerId}",
                        ct);
                    capture.StoragePath = cloudUrl;
                    _logger.LogInformation("Screenshot uploaded to Cloudinary: {Url}", cloudUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cloudinary upload failed, screenshot stored locally only");
                    if (string.IsNullOrEmpty(capture.StoragePath))
                    {
                        var screenshotDir = Path.Combine("screenshots", request.PlayerId);
                        Directory.CreateDirectory(screenshotDir);
                        var filePath = Path.Combine(screenshotDir, $"{capture.Id}.jpg");
                        await File.WriteAllBytesAsync(filePath, capture.ImageData, ct);
                        capture.StoragePath = filePath;
                    }
                }
            }

            lock (_storeLock)
            {
                if (_captureStore.Count >= 10000)
                    _captureStore.RemoveRange(0, 1000);
                _captureStore.Add(capture);
            }

            _logger.LogInformation("Screenshot captured for player {PlayerId} (event: {EventId})",
                request.PlayerId, request.DetectionEventId ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot for player {PlayerId}", request.PlayerId);
        }

        return capture;
    }

    public Task<List<ScreenshotCaptureDto>> GetHistoryAsync(string playerId, int limit = 50)
    {
        lock (_storeLock)
        {
            var results = _captureStore
                .Where(s => s.PlayerId == playerId)
                .OrderByDescending(s => s.CapturedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult(results);
        }
    }

    public Task<ScreenshotCaptureDto?> GetByIdAsync(string id)
    {
        lock (_storeLock)
        {
            var result = _captureStore.FirstOrDefault(s => s.Id == id);
            return Task.FromResult(result);
        }
    }

    public async Task<string?> StoreScreenshotAsync(ScreenshotCaptureDto screenshot)
    {
        try
        {
            var dir = Path.Combine("screenshots", screenshot.PlayerId);
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"{screenshot.Id}.{screenshot.Format}");

            if (screenshot.ImageData.Length > 0)
            {
                await File.WriteAllBytesAsync(filePath, screenshot.ImageData);
                screenshot.StoragePath = filePath;
                _logger.LogInformation("Screenshot {Id} stored to disk: {Path}", screenshot.Id, filePath);
                return filePath;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store screenshot {Id} to disk", screenshot.Id);
            return null;
        }
    }

    public bool VerifyHmac(ScreenshotCaptureDto screenshot, string secret)
    {
        if (string.IsNullOrEmpty(screenshot.HmacSignature))
            return false;

        var expected = SignScreenshot(screenshot, secret);
        return string.Equals(screenshot.HmacSignature, expected, StringComparison.OrdinalIgnoreCase);
    }

    public string SignScreenshot(ScreenshotCaptureDto screenshot, string secret)
    {
        var data = $"{screenshot.Id}|{screenshot.PlayerId}|{screenshot.CapturedAt:O}|{screenshot.DetectionEventId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static byte[] GenerateDummyScreenshot(string id, string reason)
    {
        try
        {
            var header = Encoding.UTF8.GetBytes($"SCREENSHOT:{id}:{reason}");
            using var ms = new MemoryStream(header.Length);
            ms.Write(header, 0, header.Length);
            return ms.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
