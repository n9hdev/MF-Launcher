using System.Net.Http.Json;
using System.Text.Json;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Service.Services;

public class ApiClientService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IHardwareIdProvider _hwidProvider;
    private readonly ILogger<ApiClientService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ApiClientService(
        IHardwareIdProvider hwidProvider,
        ILogger<ApiClientService> logger)
    {
        _hwidProvider = hwidProvider;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ServiceDeployConfig.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", ServiceDeployConfig.ApiKey);
    }

    private static string? ReadSessionOwner()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AntiCheat", "session_owner.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch { return null; }
    }

    public async Task<ServiceHeartbeatResponse?> SendHeartbeatAsync(ServiceHeartbeatRequest request, CancellationToken ct = default)
    {
        try
        {
            request.UserId ??= ReadSessionOwner();
            var response = await _httpClient.PostAsJsonAsync("api/service/heartbeat", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Heartbeat returned {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ServiceHeartbeatResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Heartbeat failed");
            return null;
        }
    }

    public async Task<ServiceDetectionResponse?> ReportDetectionsAsync(ServiceDetectionReport request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/service/detections", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Detection report returned {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ServiceDetectionResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report detections");
            return null;
        }
    }

    public async Task<GameHashVerifyResponse?> VerifyGameHashesAsync(GameHashVerifyRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/service/verify-hashes", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hash verification returned {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<GameHashVerifyResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hash verification failed");
            return null;
        }
    }

    public string GetHardwareId() => _hwidProvider.GetHardwareId();

    public async Task<ScreenshotUploadResponse?> UploadScreenshotAsync(ScreenshotUploadRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/service/screenshot-upload", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Screenshot upload returned {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ScreenshotUploadResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload screenshot");
            return null;
        }
    }

    public async Task<StreamFrameSubmitResponse?> SubmitStreamFrameAsync(StreamFrameSubmitRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/service/stream-frame", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stream frame submit returned {Status}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<StreamFrameSubmitResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to submit stream frame");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
