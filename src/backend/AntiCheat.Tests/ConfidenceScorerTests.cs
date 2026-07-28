using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Shared.Models;
using FluentAssertions;

namespace AntiCheat.Tests;

public class ConfidenceScorerTests
{
    private readonly ConfidenceScorer _sut = new();

    [Theory]
    [InlineData(0.5, 0, 0, 0.5)]
    [InlineData(0.9, 0, 0, 0.9)]
    [InlineData(0.0, 0, 0, 0.0)]
    [InlineData(1.0, 0, 0, 1.0)]
    public void CalculateConfidence_NoModifiers_ReturnsRawScore(double raw, int detections, int total, double expected)
    {
        var result = _sut.CalculateConfidence(raw, detections, total);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.5, 2, 100, 0.55)]
    [InlineData(0.5, 3, 100, 0.6)]
    [InlineData(0.95, 2, 100, 1.0)]
    public void CalculateConfidence_MultipleDetections_IncreasesScore(double raw, int detections, int total, double expected)
    {
        var result = _sut.CalculateConfidence(raw, detections, total);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.5, 1, 10000, 0.6)]
    [InlineData(0.5, 1, 100, 0.5)]
    [InlineData(0.8, 1, 1000, 0.9)]
    public void CalculateConfidence_LowPrevalence_BoostsScore(double raw, int detections, int total, double expected)
    {
        var result = _sut.CalculateConfidence(raw, detections, total);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.95, "critical")]
    [InlineData(0.9, "critical")]
    [InlineData(0.89, "high")]
    [InlineData(0.7, "high")]
    [InlineData(0.69, "medium")]
    [InlineData(0.4, "medium")]
    [InlineData(0.39, "low")]
    [InlineData(0.0, "low")]
    public void ClassifySeverity_ReturnsCorrectLevel(double confidence, string expected)
    {
        var result = _sut.ClassifySeverity(confidence);
        result.Should().Be(expected);
    }

    [Fact]
    public void ApplyModifiers_WithPlayerId_IncreasesScore()
    {
        var context = new DetectionContext { PlayerId = "player-1" };
        var result = _sut.ApplyModifiers(0.5, context);
        result.Should().Be(0.52);
    }

    [Fact]
    public void ApplyModifiers_WithoutPlayerId_KeepsScore()
    {
        var context = new DetectionContext();
        var result = _sut.ApplyModifiers(0.5, context);
        result.Should().Be(0.5);
    }

    [Fact]
    public void ApplyModifiers_ClampsAboveOne()
    {
        var context = new DetectionContext { PlayerId = "p1" };
        var result = _sut.ApplyModifiers(0.99, context);
        result.Should().Be(1.0);
    }
}
