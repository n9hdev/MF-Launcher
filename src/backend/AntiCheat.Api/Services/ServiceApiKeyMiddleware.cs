namespace AntiCheat.Api.Services;

public class ServiceApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceApiKeyMiddleware> _logger;

    private static readonly HashSet<string> ServiceOnlyEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "heartbeat",
        "detections",
        "screenshot-upload",
        "stream-frame",
        "verify-hashes",
    };

    public ServiceApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ServiceApiKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/service", out var remaining))
        {
            var segments = remaining.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var endpoint = segments?.Length > 0 ? segments[0] : "";

            if (ServiceOnlyEndpoints.Contains(endpoint))
            {
                var apiKeys = _configuration.GetSection("ServiceApiKeys").Get<string[]>();
                var providedKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

                if (string.IsNullOrEmpty(providedKey) || apiKeys == null || apiKeys.Length == 0 || !apiKeys.Contains(providedKey))
                {
                    _logger.LogWarning("Rejected service API request to {Endpoint} — invalid or missing key from {RemoteIp}",
                        endpoint, context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: invalid or missing X-Api-Key header");
                    return;
                }
            }
        }

        await _next(context);
    }
}
