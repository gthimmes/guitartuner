namespace Tuner.Core;

/// <summary>
/// Analyzes audio signal properties.
/// </summary>
public static class SignalAnalyzer
{
    /// <summary>
    /// Calculates the RMS (Root Mean Square) level of the signal.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <returns>RMS level (0.0 to 1.0 for normalized audio).</returns>
    public static double CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
            return 0;

        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        return Math.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// Converts stereo samples to mono by averaging channels.
    /// </summary>
    public static float[] StereoToMono(ReadOnlySpan<float> stereoSamples)
    {
        int monoLength = stereoSamples.Length / 2;
        float[] mono = new float[monoLength];

        for (int i = 0; i < monoLength; i++)
        {
            mono[i] = (stereoSamples[i * 2] + stereoSamples[i * 2 + 1]) * 0.5f;
        }

        return mono;
    }

    /// <summary>
    /// Applies a Hann window to the samples.
    /// </summary>
    public static void ApplyHannWindow(Span<float> samples)
    {
        int n = samples.Length;
        for (int i = 0; i < n; i++)
        {
            double multiplier = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            samples[i] *= (float)multiplier;
        }
    }

    /// <summary>
    /// Detects if the signal contains a transient (pluck attack).
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="threshold">Threshold for transient detection.</param>
    /// <returns>True if a transient is detected in the early part of the buffer.</returns>
    public static bool DetectTransient(ReadOnlySpan<float> samples, double threshold = 0.3)
    {
        if (samples.Length < 256)
            return false;

        // Compare energy in first 10% vs next 10%
        int chunkSize = samples.Length / 10;

        double earlyEnergy = 0;
        double laterEnergy = 0;

        for (int i = 0; i < chunkSize; i++)
        {
            earlyEnergy += samples[i] * samples[i];
            laterEnergy += samples[i + chunkSize] * samples[i + chunkSize];
        }

        // If early energy is much higher, likely a transient
        if (laterEnergy > 0)
        {
            double ratio = earlyEnergy / laterEnergy;
            return ratio > (1 + threshold);
        }

        return false;
    }

    /// <summary>
    /// Removes DC offset from the signal.
    /// </summary>
    public static void RemoveDcOffset(Span<float> samples)
    {
        if (samples.Length == 0)
            return;

        // Calculate mean
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i];
        }
        float mean = sum / samples.Length;

        // Subtract mean
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] -= mean;
        }
    }
}
