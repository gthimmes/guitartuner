using FluentAssertions;
using Tuner.AppContracts;
using Tuner.Core;
using Xunit;

namespace Tuner.Integration.Tests;

/// <summary>
/// Locks in pitch ACCURACY (not just string identity) through the full engine.
/// These use harmonic-rich tones, the case that exposed the Hann-window bias:
/// windowing before the autocorrelation/NSDF biased low strings ~+8 cents sharp.
/// If anyone re-introduces a window in the detection path, the low-E case fails.
/// </summary>
public class PitchAccuracyTests : IDisposable
{
    private readonly MockAudioInput _audioInput;
    private readonly TunerEngine _engine;
    private readonly List<TunerFrame> _frames = new();
    private readonly SemaphoreSlim _signal = new(0);

    public PitchAccuracyTests()
    {
        _audioInput = new MockAudioInput();
        _engine = new TunerEngine(_audioInput, new TunerConfiguration
        {
            WindowSize = 4096,
            HopSize = 512,
            MinConfidence = 0.7,
            MinRmsThreshold = 0.01,
        });
        _engine.FrameReady += (_, f) =>
        {
            lock (_frames) _frames.Add(f);
            _signal.Release();
        };
    }

    [Theory]
    [InlineData("E2", 82.4069)]
    [InlineData("A2", 110.0000)]
    [InlineData("D3", 146.8324)]
    [InlineData("G3", 195.9977)]
    [InlineData("B3", 246.9417)]
    [InlineData("E4", 329.6276)]
    public async Task Engine_DetectsOpenString_WithinFiveCents(string note, double exactHz)
    {
        await _engine.StartAsync();

        SimulatePluckedTone(exactHz, durationMs: 700);
        await WaitForFrames(8);

        double[] detected;
        lock (_frames)
            detected = _frames
                .Where(f => f.DetectedFrequency is > 0)
                .Select(f => f.DetectedFrequency!.Value)
                .ToArray();

        detected.Should().NotBeEmpty($"the engine should report a pitch for {note}");

        double median = Median(detected);
        double cents = 1200.0 * Math.Log2(median / exactHz);

        cents.Should().BeInRange(-5, 5,
            $"{note} ({exactHz:F2} Hz) should be detected within 5 cents, got {median:F2} Hz ({cents:+0.0;-0.0} cents)");
    }

    /// <summary>
    /// Sustained plucked-string timbre: fundamental + strong upper partials, the
    /// content that makes pitch detection non-trivial. No decay so every frame
    /// carries signal.
    /// </summary>
    private void SimulatePluckedTone(double fundamentalHz, int durationMs)
    {
        const int sampleRate = 48000;
        double[] partials = { 1.0, 0.85, 0.55, 0.35, 0.20, 0.12 };
        int totalSamples = (int)(sampleRate * durationMs / 1000.0);
        const int chunkSize = 512;

        for (int offset = 0; offset < totalSamples; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, totalSamples - offset);
            float[] chunk = new float[remaining];
            for (int i = 0; i < remaining; i++)
            {
                double t = (offset + i) / (double)sampleRate;
                double s = 0;
                for (int h = 0; h < partials.Length; h++)
                {
                    double freq = fundamentalHz * (h + 1);
                    if (freq >= sampleRate / 2.0) break;
                    s += partials[h] * Math.Sin(2 * Math.PI * freq * t);
                }
                chunk[i] = (float)(0.25 * s);
            }
            _audioInput.SimulateAudioFrame(chunk);
            Thread.Sleep(5);
        }
    }

    private async Task WaitForFrames(int count, int timeoutMs = 3000)
    {
        for (int i = 0; i < count; i++)
            if (!await _signal.WaitAsync(timeoutMs)) break;
    }

    private static double Median(double[] xs)
    {
        var s = xs.OrderBy(x => x).ToArray();
        int m = s.Length / 2;
        return s.Length % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2.0;
    }

    public void Dispose()
    {
        _engine.Dispose();
        _audioInput.Dispose();
        _signal.Dispose();
    }
}
