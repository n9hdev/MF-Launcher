using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AntiCheat.Service.Services;

public class NamedPipeService : IDisposable, IHostedService
{
    private const string PipeName = "mf-anticheat-capture";
    private readonly ILogger<NamedPipeService> _logger;
    private NamedPipeServerStream? _serverStream;
    private CancellationTokenSource _cts = new();
    private Task? _listenerTask;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _clientConnected;

    private readonly Dictionary<string, TaskCompletionSource<ScreenshotCaptureResult>> _pendingScreenshots = new();
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingStreamStarts = new();

    public bool IsClientConnected => _clientConnected;

    public NamedPipeService(ILogger<NamedPipeService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listenerTask = RunListenerAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        _clientConnected = false;
        try { _serverStream?.Dispose(); } catch { }
        if (_listenerTask != null)
            await Task.WhenAny(_listenerTask, Task.Delay(5000, cancellationToken));
    }

    private async Task RunListenerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _serverStream = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096);

                _logger.LogInformation("Named pipe server waiting for Electron connection...");
                await _serverStream.WaitForConnectionAsync(ct);
                _clientConnected = true;
                _logger.LogInformation("Electron connected to named pipe");

                await HandleClientAsync(_serverStream, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Named pipe error, reconnecting in 3s");
                _clientConnected = false;
                try { _serverStream?.Dispose(); } catch { }
                await Task.Delay(3000, ct);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && stream.IsConnected)
        {
            try
            {
                var message = await ReadMessageAsync(stream, ct);
                if (message == null) break;

                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp))
                    continue;

                var type = typeProp.GetString();
                switch (type)
                {
                    case "screenshot_result":
                        HandleScreenshotResult(root);
                        break;
                    case "stream_started":
                        HandleStreamStarted(root);
                        break;
                    case "stream_frame":
                        HandleStreamFrame(root);
                        break;
                    case "stream_stopped":
                        HandleStreamStopped(root);
                        break;
                    case "status":
                        HandleStatus(root);
                        break;
                    case "heartbeat_ack":
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                _logger.LogInformation("Electron disconnected (IO)");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Pipe message handling error");
            }
        }
        _clientConnected = false;
        _logger.LogInformation("Electron disconnected from named pipe");
    }

    public async Task<ScreenshotCaptureResult?> RequestScreenshotAsync(int quality = 80, int width = 0, int height = 0, CancellationToken ct = default)
    {
        if (!_clientConnected || _serverStream == null || !_serverStream.IsConnected)
        {
            _logger.LogDebug("Cannot request screenshot: Electron not connected");
            return null;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ScreenshotCaptureResult>();
        _pendingScreenshots[requestId] = tcs;

        ct.Register(() =>
        {
            lock (_pendingScreenshots)
            {
                _pendingScreenshots.Remove(requestId);
            }
            tcs.TrySetCanceled();
        });

        try
        {
            var request = JsonSerializer.Serialize(new
            {
                type = "capture_screenshot",
                requestId,
                quality,
                width,
                height,
            });

            await SendMessageAsync(request, ct);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            linkedCts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        catch
        {
            lock (_pendingScreenshots)
            {
                _pendingScreenshots.Remove(requestId);
            }
            throw;
        }
    }

    public async Task<bool> RequestStreamStartAsync(string sessionId, double targetFps, int jpegQuality, CancellationToken ct = default)
    {
        if (!_clientConnected || _serverStream == null || !_serverStream.IsConnected)
        {
            _logger.LogDebug("Cannot start stream: Electron not connected");
            return false;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<bool>();
        _pendingStreamStarts[requestId] = tcs;

        ct.Register(() =>
        {
            lock (_pendingStreamStarts)
            {
                _pendingStreamStarts.Remove(requestId);
            }
            tcs.TrySetCanceled();
        });

        try
        {
            var request = JsonSerializer.Serialize(new
            {
                type = "start_stream",
                requestId,
                sessionId,
                targetFps,
                jpegQuality,
            });

            await SendMessageAsync(request, ct);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            linkedCts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        catch
        {
            lock (_pendingStreamStarts)
            {
                _pendingStreamStarts.Remove(requestId);
            }
            throw;
        }
    }

    public async Task RequestStreamStopAsync(CancellationToken ct = default)
    {
        if (!_clientConnected || _serverStream == null || !_serverStream.IsConnected)
            return;

        var request = JsonSerializer.Serialize(new
        {
            type = "stop_stream",
        });

        await SendMessageAsync(request, ct);
    }

    public event Func<StreamFrameData, Task>? StreamFrameReceived;

    private void HandleScreenshotResult(JsonElement root)
    {
        var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
        if (requestId == null) return;

        lock (_pendingScreenshots)
        {
            if (_pendingScreenshots.TryGetValue(requestId, out var tcs))
            {
                _pendingScreenshots.Remove(requestId);
                var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                var imageData = root.TryGetProperty("imageData", out var d) ? d.GetString() : null;
                var width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                var height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;

                tcs.TrySetResult(new ScreenshotCaptureResult
                {
                    Success = success,
                    ImageBase64 = imageData,
                    Width = width,
                    Height = height,
                    Error = error,
                });
            }
        }
    }

    private void HandleStreamStarted(JsonElement root)
    {
        var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
        if (requestId == null) return;

        lock (_pendingStreamStarts)
        {
            if (_pendingStreamStarts.TryGetValue(requestId, out var tcs))
            {
                _pendingStreamStarts.Remove(requestId);
                var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                tcs.TrySetResult(success);
            }
        }
    }

    private void HandleStreamFrame(JsonElement root)
    {
        var sessionId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
        if (sessionId == null) return;

        StreamFrameReceived?.Invoke(new StreamFrameData
        {
            SessionId = sessionId,
            ImageBase64 = root.TryGetProperty("imageData", out var d) ? d.GetString() : null,
            Width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
            Height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
            Timestamp = DateTime.UtcNow,
        });
    }

    private void HandleStreamStopped(JsonElement root)
    {
    }

    private void HandleStatus(JsonElement root)
    {
        var version = root.TryGetProperty("version", out var v) ? v.GetString() : "unknown";
        var backend = root.TryGetProperty("captureBackend", out var b) ? b.GetString() : "unknown";
        var screenCount = root.TryGetProperty("screenCount", out var sc) ? sc.GetInt32() : 0;
        _logger.LogInformation("Electron status: version={Version}, backend={Backend}, screens={Screens}",
            version, backend, screenCount);
    }

    private async Task SendMessageAsync(string json, CancellationToken ct)
    {
        if (_serverStream == null || !_serverStream.IsConnected)
            return;

        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

        await _writeLock.WaitAsync(ct);
        try
        {
            await _serverStream.WriteAsync(lengthBytes, ct);
            await _serverStream.WriteAsync(jsonBytes, ct);
            await _serverStream.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<string?> ReadMessageAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, 0, 4, ct);
        var length = BitConverter.ToInt32(lengthBuffer, 0);

        if (length <= 0 || length > 10 * 1024 * 1024)
            return null;

        var messageBuffer = new byte[length];
        await ReadExactAsync(stream, messageBuffer, 0, length, ct);
        return Encoding.UTF8.GetString(messageBuffer);
    }

    private static async Task ReadExactAsync(NamedPipeServerStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            if (read == 0)
                throw new IOException("Pipe closed");
            totalRead += read;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _clientConnected = false;
        try { _serverStream?.Dispose(); } catch { }
        _cts.Dispose();
        _writeLock.Dispose();
    }
}

public class ScreenshotCaptureResult
{
    public bool Success { get; set; }
    public string? ImageBase64 { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Error { get; set; }
}

public class StreamFrameData
{
    public string? SessionId { get; set; }
    public string? ImageBase64 { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Timestamp { get; set; }
}
