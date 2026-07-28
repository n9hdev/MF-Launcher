using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AntiCheat.Tests;

public class EvidenceCollectorTests
{
    private readonly EvidenceCollector _sut;
    private readonly DetectionEventDto _testEvent = new()
    {
        Id = "det-event-1",
        Type = "Memory Hack",
        ProcessName = "notepad",
    };

    public EvidenceCollectorTests()
    {
        _sut = new EvidenceCollector(Mock.Of<ILogger<EvidenceCollector>>());
    }

    [Fact]
    public async Task CaptureProcessSnapshotAsync_ValidPid_ReturnsEvidenceRecord()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var result = await _sut.CaptureProcessSnapshotAsync(process.Id, _testEvent);

        result.Should().NotBeNull();
        result.DetectionEventId.Should().Be("det-event-1");
        result.EvidenceType.Should().Be("ProcessSnapshot");
        result.Data.Should().Contain("Process:");
        result.Data.Should().Contain(process.ProcessName);
    }

    [Fact]
    public async Task CaptureProcessSnapshotAsync_InvalidPid_ReturnsErrorEvidence()
    {
        var result = await _sut.CaptureProcessSnapshotAsync(-1, _testEvent);

        result.Should().NotBeNull();
        result.DetectionEventId.Should().Be("det-event-1");
        result.Data.Should().Contain("Failed to capture");
    }

    [Fact]
    public async Task CaptureProcessSnapshotAsync_NonExistentPid_ReturnsErrorEvidence()
    {
        var result = await _sut.CaptureProcessSnapshotAsync(999999, _testEvent);

        result.Should().NotBeNull();
        result.Data.Should().Contain("Failed to capture");
    }

    [Fact]
    public async Task GetEvidenceAsync_MatchingId_ReturnsEvidence()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        await _sut.CaptureProcessSnapshotAsync(process.Id, _testEvent);

        var results = await _sut.GetEvidenceAsync("det-event-1");

        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(e => e.DetectionEventId.Should().Be("det-event-1"));
    }

    [Fact]
    public async Task GetEvidenceAsync_NonMatchingId_ReturnsEmpty()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        await _sut.CaptureProcessSnapshotAsync(process.Id, _testEvent);

        var results = await _sut.GetEvidenceAsync("non-existent-id");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEvidenceAsync_NoEvidence_ReturnsEmpty()
    {
        var results = await _sut.GetEvidenceAsync("some-id");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveEvidenceToDiskAsync_ValidEvidence_WritesFile()
    {
        var evidence = new EvidenceRecordDto
        {
            DetectionEventId = "test-event",
            Id = "evidence-1",
            EvidenceType = "ProcessSnapshot",
            Data = "test data content",
        };

        var basePath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "AntiCheatTestEvidence");
        try
        {
            var filePath = await _sut.SaveEvidenceToDiskAsync(evidence, basePath);

            filePath.Should().NotBeNull();
            System.IO.File.Exists(filePath!).Should().BeTrue();

            var content = await System.IO.File.ReadAllTextAsync(filePath!);
            content.Should().Be("test data content");
        }
        finally
        {
            if (System.IO.Directory.Exists(basePath))
                System.IO.Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveEvidenceToDiskAsync_InvalidBasePath_ReturnsNull()
    {
        var evidence = new EvidenceRecordDto
        {
            DetectionEventId = "test",
            Data = "some data",
        };

        var result = await _sut.SaveEvidenceToDiskAsync(evidence, null!);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CaptureProcessSnapshotAsync_WithCancellation()
    {
        using var cts = new CancellationTokenSource();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        var result = await _sut.CaptureProcessSnapshotAsync(process.Id, _testEvent, cts.Token);

        result.Should().NotBeNull();
    }
}
