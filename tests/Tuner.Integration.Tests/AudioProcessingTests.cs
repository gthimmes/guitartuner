using FluentAssertions;
using Tuner.Core;
using Xunit;

namespace Tuner.Integration.Tests;

public class AudioProcessingTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void PitchDetection_WorksAtDifferentSampleRates(int sampleRate)
    {
        // Arrange
        var detector = new McLeodPitchDetector(minFrequency: 60, maxFrequency: 500);
        double frequency = 110.0; // A2

        int sampleCount = (int)(sampleRate * 0.1);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }

        // Act
        var result = detector.DetectPitch(samples, sampleRate);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Frequency.Should().BeApproximately(frequency, 5);
    }

    [Fact]
    public void StereoToMono_ProcessedCorrectly()
    {
        // Arrange
        const int sampleRate = 48000;
        double frequency = 110.0;
        int sampleCount = (int)(sampleRate * 0.1);

        // Generate stereo samples (same signal in both channels)
        float[] stereoSamples = new float[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            stereoSamples[i * 2] = sample;     // Left
            stereoSamples[i * 2 + 1] = sample; // Right
        }

        // Act
        float[] monoSamples = SignalAnalyzer.StereoToMono(stereoSamples);
        var detector = new McLeodPitchDetector(minFrequency: 60, maxFrequency: 500);
        var result = detector.DetectPitch(monoSamples, sampleRate);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Frequency.Should().BeApproximately(frequency, 5);
    }

    [Fact]
    public void FullPipeline_CentsCalculation_Accurate()
    {
        // Arrange
        const int sampleRate = 48000;
        double targetFrequency = 110.0; // A2
        double slightlySharp = 111.0; // About 15 cents sharp

        int sampleCount = (int)(sampleRate * 0.15);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * slightlySharp * i / sampleRate);
        }

        var detector = new McLeodPitchDetector(minFrequency: 60, maxFrequency: 500);
        var classifier = new StringClassifier();

        // Act
        var pitchResult = detector.DetectPitch(samples, sampleRate);
        var classifyResult = classifier.Classify(pitchResult.Frequency, TuningProfiles.Standard);

        // Assert
        classifyResult.IsValid.Should().BeTrue();
        classifyResult.TargetString!.Name.Should().Be("A");
        classifyResult.CentsOffset.Should().BePositive(); // Sharp
        classifyResult.CentsOffset.Should().BeApproximately(15.7, 3); // ~15.7 cents for 1 Hz sharp at 110 Hz
    }

    [Fact]
    public void BufferAccumulation_HandlesFragmentedInput()
    {
        // Arrange
        const int sampleRate = 48000;
        double frequency = 110.0;

        // Create a long signal but process in small chunks
        int totalSamples = sampleRate / 5; // 200ms
        float[] fullSignal = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            fullSignal[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }

        // Process in chunks of 256 samples
        var detector = new McLeodPitchDetector(minFrequency: 60, maxFrequency: 500);
        int windowSize = 4096;
        float[] buffer = new float[windowSize];
        int bufferIndex = 0;
        PitchDetectionResult lastResult = default;

        foreach (var chunk in fullSignal.Chunk(256))
        {
            foreach (var sample in chunk)
            {
                buffer[bufferIndex++ % windowSize] = sample;
            }

            if (bufferIndex >= windowSize)
            {
                lastResult = detector.DetectPitch(buffer, sampleRate);
            }
        }

        // Assert
        lastResult.IsValid.Should().BeTrue();
        lastResult.Frequency.Should().BeApproximately(frequency, 5);
    }
}
