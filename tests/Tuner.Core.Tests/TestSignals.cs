namespace Tuner.Core.Tests;

/// <summary>
/// Helper class for generating test audio signals.
/// </summary>
public static class TestSignals
{
    /// <summary>
    /// Generates a pure sine wave at the specified frequency.
    /// </summary>
    public static float[] GenerateSineWave(double frequency, int sampleRate, double durationSeconds)
    {
        int sampleCount = (int)(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }

        return samples;
    }

    /// <summary>
    /// Generates a sine wave with harmonics (guitar-like timbre).
    /// </summary>
    public static float[] GenerateGuitarTone(double fundamental, int sampleRate, double durationSeconds)
    {
        int sampleCount = (int)(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        // Harmonic amplitudes typical of guitar
        double[] harmonicAmplitudes = { 1.0, 0.5, 0.3, 0.2, 0.15, 0.1 };

        for (int i = 0; i < sampleCount; i++)
        {
            double sample = 0;
            for (int h = 0; h < harmonicAmplitudes.Length; h++)
            {
                double freq = fundamental * (h + 1);
                sample += harmonicAmplitudes[h] * Math.Sin(2 * Math.PI * freq * i / sampleRate);
            }
            samples[i] = (float)(sample / harmonicAmplitudes.Sum());
        }

        return samples;
    }

    /// <summary>
    /// Generates white noise.
    /// </summary>
    public static float[] GenerateWhiteNoise(int sampleCount, int seed = 42)
    {
        var random = new Random(seed);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return samples;
    }

    /// <summary>
    /// Generates silence.
    /// </summary>
    public static float[] GenerateSilence(int sampleCount)
    {
        return new float[sampleCount];
    }

    /// <summary>
    /// Adds noise to a signal.
    /// </summary>
    public static float[] AddNoise(float[] signal, double snrDb, int seed = 42)
    {
        var random = new Random(seed);
        float[] result = new float[signal.Length];

        double signalPower = signal.Sum(s => s * s) / signal.Length;
        double noisePower = signalPower / Math.Pow(10, snrDb / 10);
        double noiseAmplitude = Math.Sqrt(noisePower);

        for (int i = 0; i < signal.Length; i++)
        {
            result[i] = signal[i] + (float)((random.NextDouble() * 2 - 1) * noiseAmplitude);
        }

        return result;
    }
}
