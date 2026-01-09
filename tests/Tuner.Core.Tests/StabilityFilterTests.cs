using FluentAssertions;
using Tuner.AppContracts;
using Xunit;

namespace Tuner.Core.Tests;

public class StabilityFilterTests
{
    private readonly StabilityFilter _filter;
    private const double MinConfidence = 0.8;
    private const double MinRms = 0.01;

    public StabilityFilterTests()
    {
        _filter = new StabilityFilter(smoothingAlpha: 0.3, stabilityFrames: 5, inTuneTolerance: 5.0);
    }

    [Fact]
    public void Process_LowSignal_ReturnsTooQuiet()
    {
        // Act
        var result = _filter.Process(110.0, 0, 0.9, 0.001, MinConfidence, MinRms);

        // Assert
        result.State.Should().Be(TunerState.TooQuiet);
    }

    [Fact]
    public void Process_LowConfidence_ReturnsUnstable()
    {
        // Act
        var result = _filter.Process(110.0, 0, 0.5, 0.1, MinConfidence, MinRms);

        // Assert
        result.State.Should().Be(TunerState.Unstable);
    }

    [Fact]
    public void Process_FlatPitch_ReturnsFlat()
    {
        // Act
        var result = _filter.Process(108.0, -10, 0.9, 0.1, MinConfidence, MinRms);

        // Assert
        result.State.Should().Be(TunerState.Flat);
    }

    [Fact]
    public void Process_SharpPitch_ReturnsSharp()
    {
        // Act
        var result = _filter.Process(112.0, 10, 0.9, 0.1, MinConfidence, MinRms);

        // Assert
        result.State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void Process_InTuneAfterStabilityFrames_ReturnsInTune()
    {
        // Arrange
        var filter = new StabilityFilter(smoothingAlpha: 0.5, stabilityFrames: 3, inTuneTolerance: 5.0);

        // Act - Need consecutive in-tune frames
        filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);
        filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);
        filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);
        var result = filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);

        // Assert
        result.State.Should().Be(TunerState.InTune);
    }

    [Fact]
    public void Process_BreakInTune_ResetsCounter()
    {
        // Arrange
        var filter = new StabilityFilter(smoothingAlpha: 0.5, stabilityFrames: 3, inTuneTolerance: 5.0);

        // Establish in-tune
        for (int i = 0; i < 5; i++)
        {
            filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);
        }

        // Act - Go out of tune
        filter.Process(110.0, 20, 0.9, 0.1, MinConfidence, MinRms);

        // Try to get back in tune (but counter reset)
        var result = filter.Process(110.0, 1, 0.9, 0.1, MinConfidence, MinRms);

        // Assert - Should not be InTune yet because counter was reset
        result.State.Should().NotBe(TunerState.InTune);
    }

    [Fact]
    public void Process_Smoothing_AppliesEMA()
    {
        // Arrange
        var filter = new StabilityFilter(smoothingAlpha: 0.3, stabilityFrames: 1, inTuneTolerance: 5.0);

        // Act
        filter.Process(100.0, 0, 0.9, 0.1, MinConfidence, MinRms);
        var result = filter.Process(200.0, 0, 0.9, 0.1, MinConfidence, MinRms);

        // Assert - With alpha=0.3, smoothed = 0.3*200 + 0.7*100 = 130
        result.Frequency.Should().BeApproximately(130.0, 1.0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange - Build up some state
        _filter.Process(110.0, 0, 0.9, 0.1, MinConfidence, MinRms);
        _filter.Process(110.0, 0, 0.9, 0.1, MinConfidence, MinRms);

        // Act
        _filter.Reset();

        // Assert
        _filter.SmoothedFrequency.Should().Be(0);
        _filter.SmoothedCents.Should().Be(0);
    }
}
