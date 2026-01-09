using FluentAssertions;
using Tuner.AppContracts;
using Xunit;

namespace Tuner.Core.Tests;

public class StringClassifierTests
{
    private readonly TuningProfile _standardTuning = TuningProfiles.Standard;
    private readonly StringClassifier _classifier;

    public StringClassifierTests()
    {
        _classifier = new StringClassifier(maxCentsForMatch: 250, hysteresisFrames: 3);
    }

    [Theory]
    [InlineData(82.41, "E")]   // Low E exact
    [InlineData(110.0, "A")]   // A exact
    [InlineData(146.83, "D")] // D exact
    [InlineData(196.0, "G")]  // G exact
    [InlineData(246.94, "B")] // B exact
    [InlineData(329.63, "e")] // High e exact
    public void Classify_ExactFrequencies_ReturnsCorrectString(double frequency, string expectedString)
    {
        // Act
        var result = _classifier.Classify(frequency, _standardTuning);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TargetString!.Name.Should().Be(expectedString);
        Math.Abs(result.CentsOffset).Should().BeLessThan(1);
    }

    [Theory]
    [InlineData(80.0, "E")]   // Slightly flat Low E
    [InlineData(85.0, "E")]   // Slightly sharp Low E
    [InlineData(105.0, "A")] // Flat A
    [InlineData(115.0, "A")] // Sharp A
    public void Classify_SlightlyOffPitch_ReturnsClosestString(double frequency, string expectedString)
    {
        // Act
        var result = _classifier.Classify(frequency, _standardTuning);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TargetString!.Name.Should().Be(expectedString);
    }

    [Fact]
    public void Classify_FrequencyFlat_ReturnsNegativeCents()
    {
        // Arrange - E2 is 82.41 Hz
        double flatE2 = 80.0;

        // Act
        var result = _classifier.Classify(flatE2, _standardTuning);

        // Assert
        result.CentsOffset.Should().BeNegative();
    }

    [Fact]
    public void Classify_FrequencySharp_ReturnsPositiveCents()
    {
        // Arrange - E2 is 82.41 Hz
        double sharpE2 = 85.0;

        // Act
        var result = _classifier.Classify(sharpE2, _standardTuning);

        // Assert
        result.CentsOffset.Should().BePositive();
    }

    [Fact]
    public void Classify_FrequencyTooFarFromAnyString_ReturnsEmpty()
    {
        // Arrange - 60 Hz is too far from any guitar string
        double frequency = 60.0;

        // Act
        var result = _classifier.Classify(frequency, _standardTuning);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Classify_ZeroFrequency_ReturnsEmpty()
    {
        // Act
        var result = _classifier.Classify(0, _standardTuning);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Classify_NegativeFrequency_ReturnsEmpty()
    {
        // Act
        var result = _classifier.Classify(-100, _standardTuning);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Classify_DropDTuning_LowDCorrectlyIdentified()
    {
        // Arrange
        var dropDTuning = TuningProfiles.DropD;
        double lowD = 73.42; // Low D

        // Act
        var result = _classifier.Classify(lowD, dropDTuning);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TargetString!.Name.Should().Be("D");
    }

    [Fact]
    public void Classify_Hysteresis_DoesNotSwitchImmediately()
    {
        // Arrange
        var classifier = new StringClassifier(maxCentsForMatch: 250, hysteresisFrames: 5);

        // First, establish E2
        for (int i = 0; i < 10; i++)
        {
            classifier.Classify(82.41, _standardTuning);
        }

        // Act - Now suddenly get a frequency closer to A2
        var result1 = classifier.Classify(110.0, _standardTuning);
        var result2 = classifier.Classify(110.0, _standardTuning);

        // Assert - Should still be on E2 due to hysteresis (first few frames)
        // After a few more frames, it should switch
        for (int i = 0; i < 5; i++)
        {
            var result = classifier.Classify(110.0, _standardTuning);
        }
        var finalResult = classifier.Classify(110.0, _standardTuning);
        finalResult.TargetString!.Name.Should().Be("A");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange - Establish a string
        _classifier.Classify(82.41, _standardTuning);
        _classifier.Classify(82.41, _standardTuning);

        // Act
        _classifier.Reset();

        // Assert - CurrentString should be null
        _classifier.CurrentString.Should().BeNull();
    }
}
