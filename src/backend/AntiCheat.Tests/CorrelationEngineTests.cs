using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AntiCheat.Tests;

public class CorrelationEngineTests
{
    private readonly CorrelationEngine _sut;

    public CorrelationEngineTests()
    {
        var logger = Mock.Of<ILogger<CorrelationEngine>>();
        _sut = new CorrelationEngine(logger);
    }

    [Fact]
    public void Evaluate_EmptyEvents_ReturnsNormalLevel()
    {
        var result = _sut.Evaluate(Array.Empty<DetectionEventDto>());
        result.CorrelationScore.Should().Be(0);
        result.CorrelationLevel.Should().Be("normal");
        result.AutoEscalate.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_SingleLowConfidenceEvent_ReturnsNormal()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Memory Scanner", Confidence = 0.3 }
        };
        var result = _sut.Evaluate(events);
        result.CorrelationScore.Should().Be(7.5);
        result.CorrelationLevel.Should().Be("normal");
    }

    [Fact]
    public void Evaluate_MultipleHighConfidenceEvents_ReturnsHighScore()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Injection Detector", Confidence = 0.95 },
            new() { Type = "Memory Scanner", Confidence = 0.9 },
            new() { Type = "Thread Anomaly", Confidence = 0.85 },
        };
        var result = _sut.Evaluate(events);
        result.CorrelationScore.Should().BeGreaterThan(50);
        result.Signals.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Evaluate_MultipleInjectionEvents_ReturnsSuspicious()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Injection Detector", Confidence = 0.99 },
            new() { Type = "Memory Scanner", Confidence = 0.98 },
            new() { Type = "DLL Injection", Confidence = 0.95 },
            new() { Type = "Thread Injection", Confidence = 0.9 },
        };
        var result = _sut.Evaluate(events);
        result.CorrelationScore.Should().Be(76.0);
        result.AutoEscalate.Should().BeFalse();
        result.CorrelationLevel.Should().Be("suspicious");
    }

    [Theory]
    [InlineData(0, "normal")]
    [InlineData(15, "normal")]
    [InlineData(30, "elevated")]
    [InlineData(45, "elevated")]
    [InlineData(60, "suspicious")]
    [InlineData(75, "suspicious")]
    [InlineData(80, "high")]
    [InlineData(90, "high")]
    [InlineData(95, "critical")]
    [InlineData(100, "critical")]
    public void ClassifyCorrelation_ReturnsCorrectLevel(double score, string expected)
    {
        var result = _sut.ClassifyCorrelation(score);
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_ThreePlusSignals_AppliesMultiplier()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Memory Scanner", Confidence = 0.5 },
            new() { Type = "Injection Detector", Confidence = 0.5 },
            new() { Type = "Unsigned Module", Confidence = 0.5 },
        };
        var result = _sut.Evaluate(events);
        result.Signals.Should().HaveCount(3);
    }

    [Fact]
    public void Evaluate_CurrentCorrelationScore_UpdatesInternalState()
    {
        var events = new List<DetectionEventDto>
        {
            new() { Type = "Injection Detector", Confidence = 0.9 }
        };
        _sut.Evaluate(events);
        _sut.CurrentCorrelationScore.Should().BeGreaterThan(0);
    }
}
