using FluentAssertions;
using Xunit;

namespace Tuner.Core.Tests;

public class SignalAnalyzerTests
{
    [Fact]
    public void CalculateRms_Silence_ReturnsZero()
    {
        // Arrange
        var samples = TestSignals.GenerateSilence(1000);

        // Act
        double rms = SignalAnalyzer.CalculateRms(samples);

        // Assert
        rms.Should().Be(0);
    }

    [Fact]
    public void CalculateRms_FullScaleSine_ReturnsExpectedValue()
    {
        // Arrange - RMS of sine wave is 1/sqrt(2) ≈ 0.707
        var samples = TestSignals.GenerateSineWave(440, 48000, 0.1);

        // Act
        double rms = SignalAnalyzer.CalculateRms(samples);

        // Assert
        rms.Should().BeApproximately(0.707, 0.01);
    }

    [Fact]
    public void CalculateRms_EmptyBuffer_ReturnsZero()
    {
        // Act
        double rms = SignalAnalyzer.CalculateRms(Array.Empty<float>());

        // Assert
        rms.Should().Be(0);
    }

    [Fact]
    public void StereoToMono_ConvertsCorrectly()
    {
        // Arrange
        float[] stereo = { 0.5f, 0.5f, 1.0f, 0.0f, -0.5f, 0.5f };

        // Act
        float[] mono = SignalAnalyzer.StereoToMono(stereo);

        // Assert
        mono.Should().HaveCount(3);
        mono[0].Should().BeApproximately(0.5f, 0.001f);  // (0.5 + 0.5) / 2
        mono[1].Should().BeApproximately(0.5f, 0.001f);  // (1.0 + 0.0) / 2
        mono[2].Should().BeApproximately(0.0f, 0.001f);  // (-0.5 + 0.5) / 2
    }

    [Fact]
    public void ApplyHannWindow_ModifiesSamples()
    {
        // Arrange
        float[] samples = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };

        // Act
        SignalAnalyzer.ApplyHannWindow(samples);

        // Assert
        // First and last samples should be near zero
        samples[0].Should().BeApproximately(0, 0.01f);
        samples[4].Should().BeApproximately(0, 0.01f);
        // Middle sample should be near 1
        samples[2].Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void RemoveDcOffset_CentersSignal()
    {
        // Arrange
        float[] samples = { 1.5f, 1.5f, 1.5f, 1.5f };

        // Act
        SignalAnalyzer.RemoveDcOffset(samples);

        // Assert
        samples.Average().Should().BeApproximately(0, 0.001f);
    }

    [Fact]
    public void RemoveDcOffset_EmptyBuffer_DoesNotThrow()
    {
        // Arrange
        float[] samples = Array.Empty<float>();

        // Act
        var action = () => SignalAnalyzer.RemoveDcOffset(samples);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void DetectTransient_SuddenOnset_ReturnsTrue()
    {
        // Arrange - Simulate pluck attack
        int sampleRate = 48000;
        float[] samples = new float[4096];

        // First part has high energy (attack)
        for (int i = 0; i < 400; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }
        // Second part has lower energy
        for (int i = 400; i < 800; i++)
        {
            samples[i] = 0.3f * (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }

        // Act
        bool isTransient = SignalAnalyzer.DetectTransient(samples);

        // Assert
        isTransient.Should().BeTrue();
    }

    [Fact]
    public void DetectTransient_SteadySignal_ReturnsFalse()
    {
        // Arrange
        var samples = TestSignals.GenerateSineWave(440, 48000, 0.1);

        // Act
        bool isTransient = SignalAnalyzer.DetectTransient(samples);

        // Assert
        isTransient.Should().BeFalse();
    }
}
