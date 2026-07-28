using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;

namespace AntiCheat.Tests;

public class RiskEngineTests
{
    private readonly RiskEngine _sut = new();

    [Fact]
    public async Task AssessRiskAsync_EmptyEvents_ReturnsZeroRisk()
    {
        var result = await _sut.AssessRiskAsync(Array.Empty<DetectionEventDto>());

        result.OverallRiskScore.Should().Be(0);
        result.ActiveThreats.Should().BeEmpty();
    }

    [Fact]
    public async Task AssessRiskAsync_LowConfidenceEvents_LowRisk()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Test", Severity = "low", Confidence = 0.2 },
        };

        var result = await _sut.AssessRiskAsync(events);

        result.OverallRiskScore.Should().Be(0);
        result.RiskLevel.Should().Be("low");
    }

    [Fact]
    public async Task AssessRiskAsync_HighConfidenceCriticalEvents_HighRisk()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Injection", Severity = "critical", Confidence = 0.95 },
            new() { Type = "Memory", Severity = "high", Confidence = 0.9 },
        };

        var result = await _sut.AssessRiskAsync(events);

        result.OverallRiskScore.Should().BeGreaterThan(0.5);
        result.ActiveThreats.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AssessRiskAsync_SetsThreatTypeAndConfidence()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Memory Scanner", Severity = "high", Confidence = 0.85 },
        };

        var result = await _sut.AssessRiskAsync(events);

        var threat = result.ActiveThreats.First();
        threat.Type.Should().Be("Memory Scanner");
        threat.Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task AssessRiskAsync_ReturnsHighRiskLevel()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Injection", Severity = "critical", Confidence = 0.95 },
            new() { Type = "Memory", Severity = "high", Confidence = 0.85 },
        };

        var result = await _sut.AssessRiskAsync(events);

        result.RiskLevel.Should().BeOneOf("high", "critical");
        result.DetectorContributions.Should().ContainKeys("Injection", "Memory");
    }

    [Fact]
    public void CalculatePlayerRiskScore_EmptyHistory_ReturnsZero()
    {
        var score = _sut.CalculatePlayerRiskScore("player-1", Enumerable.Empty<DetectionEventDto>());
        score.Should().Be(0);
    }

    [Fact]
    public void CalculatePlayerRiskScore_RecentEvents_ReturnsScore()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "A", Severity = "critical", Confidence = 1.0, Timestamp = DateTime.UtcNow },
            new() { Type = "B", Severity = "high", Confidence = 0.8, Timestamp = DateTime.UtcNow.AddDays(-1) },
        };
        var score = _sut.CalculatePlayerRiskScore("player-1", events);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculatePlayerRiskScore_IgnoresOldEvents()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Old", Severity = "critical", Confidence = 1.0, Timestamp = DateTime.UtcNow.AddDays(-60) },
        };
        var score = _sut.CalculatePlayerRiskScore("player-1", events);
        score.Should().Be(0);
    }

    [Fact]
    public void ClassifyRiskLevel_ReturnsCorrectLevel()
    {
        _sut.ClassifyRiskLevel(0.9).Should().Be("critical");
        _sut.ClassifyRiskLevel(0.65).Should().Be("high");
        _sut.ClassifyRiskLevel(0.35).Should().Be("medium");
        _sut.ClassifyRiskLevel(0.1).Should().Be("low");
    }
}
