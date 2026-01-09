using FluentAssertions;
using Xunit;

namespace Tuner.UI.Tests;

/// <summary>
/// Basic application startup tests.
/// These are kept minimal as per the test pyramid.
/// Full UI automation tests would use FlaUI but are skipped for CI efficiency.
/// </summary>
public class ApplicationTests
{
    [Fact]
    public void TunerConfiguration_DefaultsAreValid()
    {
        // Arrange & Act
        var config = Tuner.AppContracts.TunerConfiguration.Default;

        // Assert - Validate defaults are sensible
        config.InTuneTolerance.Should().BePositive();
        config.StabilityFrames.Should().BePositive();
        config.SmoothingAlpha.Should().BeInRange(0, 1);
        config.MinConfidence.Should().BeInRange(0, 1);
        config.MinRmsThreshold.Should().BePositive();
        config.WindowSize.Should().BeGreaterOrEqualTo(256);
        config.HopSize.Should().BePositive();
    }

    [Fact]
    public void TunerConfiguration_Validate_ThrowsOnInvalidValues()
    {
        // Arrange
        var config = new Tuner.AppContracts.TunerConfiguration
        {
            InTuneTolerance = -1 // Invalid
        };

        // Act & Assert
        var action = () => config.Validate();
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TuningProfiles_AllHaveValidConfiguration()
    {
        // Arrange
        var profiles = Tuner.Core.TuningProfiles.All;

        // Assert
        foreach (var profile in profiles)
        {
            profile.Name.Should().NotBeNullOrEmpty();
            profile.Strings.Should().HaveCount(6, $"Profile {profile.Name} should have 6 strings");
            profile.InTuneTolerance.Should().BePositive();

            foreach (var str in profile.Strings)
            {
                str.Name.Should().NotBeNullOrEmpty();
                str.Frequency.Should().BePositive();
                str.StringNumber.Should().BePositive();
            }
        }
    }

    [Fact]
    public void AudioDevice_EqualityWorksCorrectly()
    {
        // Arrange
        var device1 = new Tuner.AppContracts.AudioDevice("id-1", "Mic 1");
        var device2 = new Tuner.AppContracts.AudioDevice("id-1", "Mic 1");
        var device3 = new Tuner.AppContracts.AudioDevice("id-2", "Mic 2");

        // Assert
        device1.Should().Be(device2);
        device1.Should().NotBe(device3);
        device1.GetHashCode().Should().Be(device2.GetHashCode());
    }

    [Fact]
    public void TunerFrame_Empty_HasDefaultValues()
    {
        // Act
        var frame = Tuner.AppContracts.TunerFrame.Empty;

        // Assert
        frame.DetectedFrequency.Should().BeNull();
        frame.Confidence.Should().Be(0);
        frame.State.Should().Be(Tuner.AppContracts.TunerState.Unknown);
        frame.CentsOffset.Should().Be(0);
        frame.TargetString.Should().BeNull();
    }

    [Fact]
    public void TunerFrame_ToString_FormatsCorrectly()
    {
        // Arrange
        var frame = new Tuner.AppContracts.TunerFrame
        {
            State = Tuner.AppContracts.TunerState.Flat,
            DetectedFrequency = 108.5,
            CentsOffset = -12.5,
            Confidence = 0.95,
            TargetString = new Tuner.AppContracts.StringTarget("A2", 110.0, 5)
        };

        // Act
        var str = frame.ToString();

        // Assert
        str.Should().Contain("Flat");
        str.Should().Contain("A2");
        str.Should().Contain("-12.5");
    }
}
