namespace AntiCheat.Service.Services;

public class ServiceScreenCapture : IDisposable
{
    private readonly ApiClientService _apiClient;
    private readonly NamedPipeService _pipeService;
    private readonly ILogger<ServiceScreenCapture> _logger;
    private volatile bool _isStreaming;
    private string? _activeStreamSessionId;
    private double _targetFps = 2;
    private int _jpegQuality = 60;

    public ServiceScreenCapture(
        ApiClientService apiClient,
        NamedPipeService pipeService,
        ILogger<ServiceScreenCapture> logger)
    {
        _apiClient = apiClient;
        _pipeService = pipeService;
        _logger = logger;

        _pipeService.StreamFrameReceived += OnStreamFrameReceived;
    }

    private async Task OnStreamFrameReceived(StreamFrameData frame)
    {
        if (!_isStreaming || string.IsNullOrEmpty(frame.ImageBase64))
            return;

        try
        {
            await _apiClient.SubmitStreamFrameAsync(new StreamFrameSubmitRequest
            {
                HardwareId = _apiClient.GetHardwareId(),
                SessionId = frame.SessionId ?? _activeStreamSessionId ?? "",
                ImageBase64 = frame.ImageBase64,
                Format = "jpeg",
                Width = frame.Width,
                Height = frame.Height,
                Timestamp = frame.Timestamp,
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to submit stream frame to API");
        }
    }

    public async Task<string?> CaptureAndUploadAsync(string? detectionEventId = null, string reason = "detection", CancellationToken ct = default)
    {
        try
        {
            if (!_pipeService.IsClientConnected)
            {
                _logger.LogWarning("Cannot capture screenshot: Electron app not connected");
                return null;
            }

            _logger.LogInformation("Requesting screenshot capture via Electron");

            var result = await _pipeService.RequestScreenshotAsync(
                quality: 80,
                width: 1920,
                height: 1080,
                ct: ct);

            if (result == null || !result.Success || string.IsNullOrEmpty(result.ImageBase64))
            {
                _logger.LogWarning("Screenshot capture failed: {Error}", result?.Error ?? "no result");
                return null;
            }

            var uploadResult = await _apiClient.UploadScreenshotAsync(new ScreenshotUploadRequest
            {
                HardwareId = _apiClient.GetHardwareId(),
                DetectionEventId = detectionEventId,
                Reason = reason,
                ImageBase64 = result.ImageBase64,
                Format = "jpeg",
                Width = result.Width,
                Height = result.Height,
                CapturedAt = DateTime.UtcNow,
            }, ct);

            if (uploadResult != null && uploadResult.Success)
            {
                _logger.LogInformation("Screenshot captured and uploaded (id: {Id}, url: {Url})",
                    uploadResult.ScreenshotId, uploadResult.CloudUrl ?? "local-only");
                return uploadResult.CloudUrl;
            }

            _logger.LogWarning("Screenshot upload failed: {Error}", uploadResult?.Error ?? "unknown");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Screenshot capture timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture and upload screenshot");
            return null;
        }
    }

    public async Task StartStreaming(string sessionId, double targetFps, int jpegQuality)
    {
        _activeStreamSessionId = sessionId;
        _targetFps = Math.Clamp(targetFps, 1.0, 15.0);
        _jpegQuality = Math.Clamp(jpegQuality, 30, 90);

        if (!_pipeService.IsClientConnected)
        {
            _logger.LogWarning("Cannot start stream: Electron app not connected");
            return;
        }

        _logger.LogInformation("Requesting stream start via Electron: session={Session}, fps={Fps}, quality={Quality}",
            sessionId, _targetFps, _jpegQuality);

        var started = await _pipeService.RequestStreamStartAsync(sessionId, _targetFps, _jpegQuality);
        if (started)
        {
            _isStreaming = true;
            _logger.LogInformation("Stream capture started by Electron");
        }
        else
        {
            _logger.LogWarning("Stream start request failed or timed out");
        }
    }

    public async Task StopStreaming()
    {
        if (_isStreaming)
        {
            await _pipeService.RequestStreamStopAsync();
            _logger.LogInformation("Stream stop request sent to Electron");
        }

        _isStreaming = false;
        _activeStreamSessionId = null;
    }

    public bool IsStreaming => _isStreaming;
    public string? ActiveSessionId => _activeStreamSessionId;
    public double CurrentFps => _targetFps;

    public void Dispose()
    {
        _pipeService.StreamFrameReceived -= OnStreamFrameReceived;
    }
}

public class ScreenshotUploadRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? DetectionEventId { get; set; }
    public string? Reason { get; set; }
    public string ImageBase64 { get; set; } = string.Empty;
    public string Format { get; set; } = "jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

public class ScreenshotUploadResponse
{
    public bool Success { get; set; }
    public string? CloudUrl { get; set; }
    public string? ScreenshotId { get; set; }
    public string? Error { get; set; }
}

public class StreamFrameSubmitRequest
{
    public string HardwareId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public string Format { get; set; } = "jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class StreamFrameSubmitResponse
{
    public bool Success { get; set; }
    public int FrameNumber { get; set; }
    public double TargetFps { get; set; }
    public bool ShouldStop { get; set; }
}
