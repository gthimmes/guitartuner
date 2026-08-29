using FluentAssertions;
using Xunit;

namespace Tuner.Core.Tests;

public class ReferenceToneSynthesizerTests
{
    private const int SampleRate = 48000;

    private static float[] Render(ReferenceToneSynthesizer synth, int samples)
    {
        var buffer = new float[samples];
        synth.Read(buffer, 0, samples);
        return buffer;
    }

    [Theory]
    [InlineData(82.4069)]   // E2
    [InlineData(110.0)]     // A2
    [InlineData(146.8324)]  // D3
    [InlineData(196.0)]     // G3
    [InlineData(246.9417)]  // B3
    [InlineData(329.6276)]  // E4
    public void Tone_IsDetectedAtTheRequestedPitch(double frequency)
    {
        var synth = new ReferenceToneSynthesizer(SampleRate) { Frequency = frequency };
        synth.Start();

        // Skip the fade-in, then analyse a steady window with the real detector.
        Render(synth, SampleRate / 2);
        var window = Render(synth, 4096);
        var detector = new McLeodPitchDetector(minFrequency: 70, maxFrequency: 400);

        var result = detector.DetectPitch(window, SampleRate);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.9);
        double cents = 1200 * Math.Log2(result.Frequency / frequency);
        cents.Should().BeApproximately(0, 1.0, $"reference tone for {frequency} Hz must be dead on");
    }

    [Fact]
    public void Output_FadesInFromSilence_AndNeverClips()
    {
        var synth = new ReferenceToneSynthesizer(SampleRate) { Frequency = 110 };
        synth.Start();

        var first = Render(synth, 2000);

        Math.Abs(first[0]).Should().BeLessThan(0.01f, "the tone ramps up instead of clicking on");
        first.Max(v => Math.Abs(v)).Should().BeLessThan(0.6f, "the tone must stay well below full scale");
        first.Skip(1500).Max(v => Math.Abs(v)).Should().BeGreaterThan(0.1f, "after the ramp the tone is audible");
    }

    [Fact]
    public void Stop_FadesOutToSilence()
    {
        var synth = new ReferenceToneSynthesizer(SampleRate) { Frequency = 110 };
        synth.Start();
        Render(synth, 4800);

        synth.Stop();
        var tail = Render(synth, 4800);

        synth.IsSilent.Should().BeTrue();
        tail.Skip(2400).Should().OnlyContain(s => s == 0f, "output is silent once the fade finishes");
        // The fade itself is gradual: no sample-to-sample jump larger than the tone's own slope
        for (int i = 1; i < 1000; i++)
            Math.Abs(tail[i] - tail[i - 1]).Should().BeLessThan(0.05f);
    }

    [Fact]
    public void Silent_ProducesZeros()
    {
        var synth = new ReferenceToneSynthesizer(SampleRate) { Frequency = 110 };

        var buffer = Render(synth, 1000);

        buffer.Should().OnlyContain(s => s == 0f);
        synth.IsSilent.Should().BeTrue();
        synth.IsOn.Should().BeFalse();
    }

    [Fact]
    public void Frequency_RejectsNonPositive()
    {
        var synth = new ReferenceToneSynthesizer(SampleRate);

        var act = () => synth.Frequency = 0;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
