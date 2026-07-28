using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Services;

public class RemoteApiService : IRemoteApiService
{
    private readonly HttpClient _httpClient;
    private readonly RemoteApiSettings _settings;
    private readonly ILogger<RemoteApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RemoteApiService(
        HttpClient httpClient,
        IOptions<RemoteApiSettings> settings,
        ILogger<RemoteApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<HeartbeatResponse?> SendHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/v2/priv8/heartbeat", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<HeartbeatResponse>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Heartbeat send failed (will retry next cycle)");
            return null;
        }
    }

    public async Task<GlobalVerifyResponse?> VerifyAsync(GlobalVerifyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            var response = await _httpClient.PostAsJsonAsync("/v2/priv8/globalverify", request, linkedCts.Token);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GlobalVerifyResponse>(body, _jsonOptions);
            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("GlobalVerify request timed out");
            return new GlobalVerifyResponse { Success = false, Error = "Request timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GlobalVerify connection failed");
            return new GlobalVerifyResponse { Success = false, Error = $"Connection failed: {ex.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GlobalVerify failed");
            return new GlobalVerifyResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> ReportAsync(AntiReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/v2/priv8/antireport", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send anti-report");
            return false;
        }
    }

    public async Task<UpdateCheckResponse?> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/v2/priv8/anticheat/update-check?current={Uri.EscapeDataString(currentVersion)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<UpdateCheckResponse>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            return null;
        }
    }

    public async Task<PlayerProfileResponse?> GetProfileAsync(string hwid, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return null;

        try
        {
            var encoded = Uri.EscapeDataString(hwid);
            var response = await _httpClient.GetAsync($"/v2/priv8/getprofile?hwid={encoded}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<PlayerProfileResponse>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch player profile");
            return null;
        }
    }

}
