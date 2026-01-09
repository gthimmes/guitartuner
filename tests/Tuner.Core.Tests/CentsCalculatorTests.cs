using FluentAssertions;
using Xunit;

namespace Tuner.Core.Tests;

public class CentsCalculatorTests
{
    [Fact]
    public void CalculateCents_ExactMatch_ReturnsZero()
    {
        // Arrange
        double frequency = 440.0;

        // Act
        double cents = CentsCalculator.CalculateCents(frequency, frequency);

        // Assert
        cents.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateCents_OneSemitoneHigher_Returns100()
    {
        // Arrange
        double target = 440.0; // A4
        double detected = 466.16; // A#4

        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().BeApproximately(100, 1);
    }

    [Fact]
    public void CalculateCents_OneSemitoneLower_ReturnsMinus100()
    {
        // Arrange
        double target = 440.0; // A4
        double detected = 415.30; // G#4

        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().BeApproximately(-100, 1);
    }

    [Fact]
    public void CalculateCents_OneOctaveHigher_Returns1200()
    {
        // Arrange
        double target = 440.0;
        double detected = 880.0;

        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().BeApproximately(1200, 0.1);
    }

    [Fact]
    public void CalculateCents_SlightlyFlat_ReturnsNegative()
    {
        // Arrange
        double target = 110.0; // A2
        double detected = 109.0; // Slightly flat

        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().BeNegative();
    }

    [Fact]
    public void CalculateCents_SlightlySharp_ReturnsPositive()
    {
        // Arrange
        double target = 110.0; // A2
        double detected = 111.0; // Slightly sharp

        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().BePositive();
    }

    [Theory]
    [InlineData(0, 440)]
    [InlineData(440, 0)]
    [InlineData(0, 0)]
    public void CalculateCents_InvalidFrequencies_ReturnsZero(double detected, double target)
    {
        // Act
        double cents = CentsCalculator.CalculateCents(detected, target);

        // Assert
        cents.Should().Be(0);
    }

    [Theory]
    [InlineData(440.0, "A4")]
    [InlineData(261.63, "C4")]
    [InlineData(82.41, "E2")]
    [InlineData(110.0, "A2")]
    [InlineData(329.63, "E4")]
    public void FrequencyToNoteName_StandardFrequencies_ReturnsCorrectName(double frequency, string expectedNote)
    {
        // Act
        string noteName = CentsCalculator.FrequencyToNoteName(frequency);

        // Assert
        noteName.Should().Be(expectedNote);
    }

    [Fact]
    public void FrequencyToNoteName_ZeroFrequency_ReturnsQuestionMark()
    {
        // Act
        string noteName = CentsCalculator.FrequencyToNoteName(0);

        // Assert
        noteName.Should().Be("?");
    }

    [Theory]
    [InlineData(442.0, 440.0)] // A4 slightly sharp
    [InlineData(220.5, 220.0)] // A3 slightly sharp
    public void GetNearestStandardFrequency_SlightlyOffPitch_ReturnsNearestStandard(double input, double expected)
    {
        // Act
        double result = CentsCalculator.GetNearestStandardFrequency(input);

        // Assert
        result.Should().BeApproximately(expected, 0.5);
    }
}
