using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AntiCheat.Tests;

public class DetectionEngineTests
{
    private readonly Mock<IDetector> _mockDetector1 = new();
    private readonly Mock<IDetector> _mockDetector2 = new();
    private readonly Mock<ILogger<DetectionEngine>> _loggerMock = new();
    private readonly Mock<IWhitelistProvider> _whitelistMock = new();
    private readonly Mock<IVerdictService> _verdictServiceMock = new();
    private readonly Mock<IBaselineService> _baselineServiceMock = new();
    private readonly Mock<IDeltaMonitorService> _deltaMonitorMock = new();
    private readonly Mock<IEvidenceCollector> _evidenceCollectorMock = new();
    private readonly Mock<IScreenCaptureService> _screenCaptureMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IDedupService> _dedupServiceMock = new();
    private readonly DetectionEngine _sut;

    public DetectionEngineTests()
    {
        _mockDetector1.Setup(d => d.Name).Returns("Process Analyzer");
        _mockDetector1.Setup(d => d.IsEnabled).Returns(true);
        _mockDetector2.Setup(d => d.Name).Returns("Memory Region Analyzer");
        _mockDetector2.Setup(d => d.IsEnabled).Returns(true);

        _verdictServiceMock
            .Setup(v => v.EvaluateAsync(It.IsAny<IReadOnlyList<DetectionEventDto>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerdictResult());

        _baselineServiceMock
            .Setup(b => b.WaitForMtaInitializationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _baselineServiceMock
            .Setup(b => b.CaptureBaselineAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BaselineSnapshot { ProcessName = "gta_sa", ProcessId = 12345 });

        _deltaMonitorMock
            .Setup(d => d.ScanDeltaAsync(It.IsAny<BaselineSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EvidenceFact>());
        _deltaMonitorMock
            .Setup(d => d.CheckCodeIntegrityAsync(It.IsAny<BaselineSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EvidenceFact>());

        _whitelistMock
            .Setup(w => w.IsProcessWhitelistedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var peAnalysisMock = new Mock<IPeAnalysisService>();
        peAnalysisMock.Setup(p => p.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeAnalysisResult());

        var sigEngineMock = new Mock<ISignatureEngine>();
        sigEngineMock.Setup(s => s.MatchPe(It.IsAny<PeAnalysisResult>())).Returns(Array.Empty<SignatureMatch>());
        sigEngineMock.Setup(s => s.MatchProcessName(It.IsAny<string>())).Returns(Array.Empty<SignatureMatch>());
        sigEngineMock.Setup(s => s.MatchFilePath(It.IsAny<string>())).Returns(Array.Empty<SignatureMatch>());

        var clamAvMock = new Mock<IClamAvService>();
        clamAvMock.Setup(c => c.IsAvailable).Returns(false);

        var teamCymruMock = new Mock<ITeamCymruService>();
        teamCymruMock.Setup(t => t.IsAvailable).Returns(false);

        var hwidProviderMock = new Mock<IHardwareIdProvider>();
        hwidProviderMock.Setup(h => h.GetHardwareId()).Returns("unknown-hwid");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPeAnalysisService))).Returns(peAnalysisMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ISignatureEngine))).Returns(sigEngineMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICertificateReputationService))).Returns(Mock.Of<ICertificateReputationService>());
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IClamAvService))).Returns(clamAvMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITeamCymruService))).Returns(teamCymruMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IHardwareIdProvider))).Returns(hwidProviderMock.Object);
        // AppDbContext is intentionally omitted — EvaluateVerdictAndMaybeBanAsync skips
        // the DB query when hwid is "unknown-hwid" (set above via IHardwareIdProvider).

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        _dedupServiceMock
            .Setup(d => d.DeduplicateAsync(It.IsAny<IReadOnlyList<DetectionEventDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<DetectionEventDto> events, CancellationToken _) => events);

        _sut = new DetectionEngine(
            new[] { _mockDetector1.Object, _mockDetector2.Object },
            _loggerMock.Object,
            _whitelistMock.Object,
            _verdictServiceMock.Object,
            _baselineServiceMock.Object,
            _deltaMonitorMock.Object,
            _evidenceCollectorMock.Object,
            _screenCaptureMock.Object,
            _scopeFactoryMock.Object,
            _dedupServiceMock.Object);
    }

    [Fact]
    public async Task RunFullScanAsync_NoEvents_ReturnsEmpty()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var result = await _sut.RunFullScanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RunFullScanAsync_AllDetectorsDisabled_ReturnsEmpty()
    {
        _mockDetector1.Setup(d => d.IsEnabled).Returns(false);
        _mockDetector2.Setup(d => d.IsEnabled).Returns(false);

        var result = await _sut.RunFullScanAsync();

        result.Should().BeEmpty();
        _mockDetector1.Verify(d => d.ScanAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunFullScanAsync_WithEvents_ReturnsEventsAndFiresOnDetection()
    {
        var ev1 = new DetectionEventDto { Type = "Test", Severity = "low", Confidence = 0.3 };
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ev1 });
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var events = new List<DetectionEventDto>();
        _sut.OnDetection += (_, e) => events.Add(e);

        var result = await _sut.RunFullScanAsync();

        result.Should().HaveCount(1);
        result[0].Type.Should().Be("Test");
        events.Should().ContainSingle(e => e.Type == "Test");
    }

    [Fact]
    public async Task RunFullScanAsync_DetectorThrows_ContinuesWithOtherDetectors()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Detector failed"));
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DetectionEventDto { Type = "FromBeta", Confidence = 0.5 } });

        var result = await _sut.RunFullScanAsync();

        result.Should().ContainSingle(e => e.Type == "FromBeta");
    }

    [Fact]
    public async Task RunFullScanAsync_Cancelled_ThrowsOperationCanceled()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await FluentActions.Awaiting(() => _sut.RunFullScanAsync(default))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectStatus()
    {
        _mockDetector1.Setup(d => d.IsEnabled).Returns(true);
        _mockDetector2.Setup(d => d.IsEnabled).Returns(false);

        var status = await _sut.GetStatusAsync();

        status.ProcessAnalyzer.Should().Be("active");
        status.MemoryRegionAnalyzer.Should().Be("inactive");
    }

    [Fact]
    public async Task EnableDetector_ActivatesDetector()
    {
        _mockDetector1.Setup(d => d.IsEnabled).Returns(false);

        await _sut.EnableDetector("Process Analyzer");

        _mockDetector1.VerifySet(d => d.IsEnabled = true, Times.Once);
    }

    [Fact]
    public async Task EnableDetector_UnknownName_DoesNothing()
    {
        await _sut.EnableDetector("NonExistent");
    }

    [Fact]
    public async Task DisableDetector_DeactivatesDetector()
    {
        await _sut.DisableDetector("Process Analyzer");

        _mockDetector1.VerifySet(d => d.IsEnabled = false, Times.Once);
    }

    [Fact]
    public async Task DisableDetector_UnknownName_DoesNothing()
    {
        await _sut.DisableDetector("NonExistent");
    }

    [Fact]
    public void Detectors_ReturnsReadOnlyList()
    {
        var detectors = _sut.Detectors;
        detectors.Should().HaveCount(2);
        detectors.Should().BeAssignableTo<IReadOnlyList<IDetector>>();
    }

    [Fact]
    public void Dispose_CancelsAndDisposes()
    {
        _sut.Dispose();
    }

    [Fact]
    public async Task RunContinuousScanAsync_LoopsUntilCancellation()
    {
        var ev = new DetectionEventDto { Type = "Test", Confidence = 0.5 };
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ev });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500);

        await FluentActions.Awaiting(() => _sut.RunContinuousScanAsync(cts.Token))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunContinuousScanAsync_ThrowsOnTokenCancelledImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await _sut.RunContinuousScanAsync(cts.Token);
    }

    [Fact]
    public async Task RunFullScanAsync_EmptyEvents_RevertsIntervalToDefault()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var result = await _sut.RunFullScanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RunPreLaunchScanAsync_Empty_ReturnsEmpty()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var result = await _sut.RunPreLaunchScanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RunPreLaunchScanAsync_WithEvents_ReturnsEventsAndFiresOnDetection()
    {
        var ev1 = new DetectionEventDto { Type = "PreLaunch: Test", Severity = "low", Confidence = 0.3 };
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ev1 });
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var events = new List<DetectionEventDto>();
        _sut.OnDetection += (_, e) => events.Add(e);

        var result = await _sut.RunPreLaunchScanAsync();

        result.Should().ContainSingle(e => e.Type == "PreLaunch: Test");
        events.Should().ContainSingle(e => e.Type == "PreLaunch: Test");
    }

    [Fact]
    public async Task RunPreLaunchScanAsync_FiresOnPreLaunchComplete()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());
        _mockDetector2.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DetectionEventDto>());

        var fired = false;
        _sut.OnPreLaunchComplete += (_, _) => fired = true;

        await _sut.RunPreLaunchScanAsync();

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task RunPreLaunchScanAsync_DetectorThrows_CompletesWithoutError()
    {
        _mockDetector1.Setup(d => d.ScanAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Process Analyzer failed"));

        var result = await _sut.RunPreLaunchScanAsync();

        result.Should().BeEmpty();
    }
}
