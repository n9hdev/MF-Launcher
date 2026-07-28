using System.Net;
using System.Text.Json;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace AntiCheat.Tests;

public class RemoteApiServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly RemoteApiService _sut;
    private readonly RemoteApiSettings _settings = new()
    {
        BaseUrl = "http://test-api:9000",
        ApiKey = "test-api-key",
        Enabled = true,
        RequestTimeoutSeconds = 10,
    };

    public RemoteApiServiceTests()
    {
        var httpClient = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri(_settings.BaseUrl) };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _settings.ApiKey);

        _sut = new RemoteApiService(
            httpClient,
            Options.Create(_settings),
            Mock.Of<ILogger<RemoteApiService>>());
    }

    [Fact]
    public async Task SendHeartbeatAsync_ReturnsResponse()
    {
        var response = new HeartbeatResponse { Success = true, Message = "Heartbeat received" };
        SetupMockResponse("/v2/priv8/heartbeat", response);

        var result = await _sut.SendHeartbeatAsync(new HeartbeatRequest
        {
            Hwid = "test-hwid",
            Status = "online",
            Timestamp = DateTime.UtcNow,
            GameRunning = false,
        });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Be("Heartbeat received");
    }

    [Fact]
    public async Task VerifyAsync_ValidRequest_ReturnsSuccess()
    {
        var response = new GlobalVerifyResponse { Success = true };
        SetupMockResponse("/v2/priv8/globalverify", response);

        var result = await _sut.VerifyAsync(new GlobalVerifyRequest
        {
            Serial = "test-serial",
            Hwid = "test-hwid",
            Hardware = new HardwareFingerprint(),
        });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_BannedPlayer_ReturnsBanReason()
    {
        var response = new GlobalVerifyResponse
        {
            Success = false,
            Error = "BANNED",
            BanReason = "Cheating detected",
        };
        SetupMockResponse("/v2/priv8/globalverify", response);

        var result = await _sut.VerifyAsync(new GlobalVerifyRequest());

        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.BanReason.Should().Be("Cheating detected");
    }

    [Fact]
    public async Task ReportAsync_ReturnsTrueOnSuccess()
    {
        SetupMockResponse("/v2/priv8/antireport", new { }, HttpStatusCode.OK);

        var result = await _sut.ReportAsync(new AntiReportRequest());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReportAsync_ReturnsFalseOnFailure()
    {
        SetupMockResponse("/v2/priv8/antireport", new { }, HttpStatusCode.InternalServerError);

        var result = await _sut.ReportAsync(new AntiReportRequest());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsUpdateInfo()
    {
        var response = new UpdateCheckResponse
        {
            HasUpdate = true,
            LatestVersion = "6.1.0",
            DownloadUrl = "http://update.example.com",
            Critical = false,
        };
        SetupMockResponse("/v2/priv8/anticheat/update-check", response);

        var result = await _sut.CheckForUpdatesAsync("6.0.0");

        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("6.1.0");
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsProfile()
    {
        var response = new PlayerProfileResponse
        {
            Success = true,
            Username = "TestPlayer",
            Status = "online",
        };
        SetupMockResponse("/v2/priv8/getprofile", response);

        var result = await _sut.GetProfileAsync("test-hwid");

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Username.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsNullOnApiError()
    {
        SetupMockResponse("/v2/priv8/getprofile", new { }, HttpStatusCode.NotFound);

        var result = await _sut.GetProfileAsync("test-hwid");

        result.Should().BeNull();
    }

    private void SetupMockResponse(string url, object responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseBody);
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.Contains(url)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
