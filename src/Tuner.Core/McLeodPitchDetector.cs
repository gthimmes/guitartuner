namespace Tuner.Core;

/// <summary>
/// McLeod Pitch Method (MPM) implementation for pitch detection.
/// Optimized for guitar fundamental frequency detection.
/// </summary>
public sealed class McLeodPitchDetector : IPitchDetector
{
    private const double DefaultCutoff = 0.7;  // Lower threshold to include fundamental
    private const double SmallCutoff = 0.5;

    private readonly double _cutoff;
    private readonly double _smallCutoff;
    private readonly double _minFrequency;
    private readonly double _maxFrequency;

    private double[]? _nsdf;

    public McLeodPitchDetector(
        double minFrequency = 70.0,
        double maxFrequency = 400.0,
        double cutoff = DefaultCutoff,
        double smallCutoff = SmallCutoff)
    {
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;
        _cutoff = cutoff;
        _smallCutoff = smallCutoff;
    }

    public PitchDetectionResult DetectPitch(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (samples.Length < 256)
            return PitchDetectionResult.Empty;

        int n = samples.Length;
        EnsureBufferSize(n);

        // Compute the Normalized Square Difference Function (NSDF)
        ComputeNsdf(samples, _nsdf!);

        // Find the best peak
        var (peakIndex, peakValue) = FindBestPeak(_nsdf!, n, sampleRate);

        if (peakIndex <= 0 || peakValue < _smallCutoff)
            return PitchDetectionResult.Empty;

        // Parabolic interpolation for sub-sample accuracy
        double refinedIndex = ParabolicInterpolation(_nsdf!, peakIndex);

        // Convert lag to frequency
        double frequency = sampleRate / refinedIndex;

        // Validate frequency range
        if (frequency < _minFrequency || frequency > _maxFrequency)
            return PitchDetectionResult.Empty;

        return new PitchDetectionResult
        {
            Frequency = frequency,
            Confidence = Math.Clamp(peakValue, 0, 1)
        };
    }

    private void EnsureBufferSize(int n)
    {
        if (_nsdf == null || _nsdf.Length < n)
        {
            _nsdf = new double[n];
        }
    }

    private void ComputeNsdf(ReadOnlySpan<float> samples, double[] nsdf)
    {
        int n = samples.Length;

        for (int tau = 0; tau < n; tau++)
        {
            double acf = 0;
            double m = 0;

            for (int i = 0; i < n - tau; i++)
            {
                acf += samples[i] * samples[i + tau];
                m += samples[i] * samples[i] + samples[i + tau] * samples[i + tau];
            }

            nsdf[tau] = m > 0 ? 2.0 * acf / m : 0;
        }
    }

    private (int index, double value) FindBestPeak(double[] nsdf, int n, int sampleRate)
    {
        // Calculate lag range based on frequency limits
        int maxLag = Math.Min(n - 1, (int)(sampleRate / _minFrequency));
        int minLag = Math.Max(1, (int)(sampleRate / _maxFrequency));

        // Find all peaks after positive-going zero crossings
        var peaks = new List<(int index, double value)>();
        bool wasNegative = true;
        int currentPeakIndex = -1;
        double currentPeakValue = double.MinValue;

        for (int i = minLag; i < maxLag; i++)
        {
            if (nsdf[i] < 0)
            {
                // Save previous peak if valid
                if (currentPeakIndex > 0 && currentPeakValue > _smallCutoff)
                {
                    peaks.Add((currentPeakIndex, currentPeakValue));
                }
                wasNegative = true;
                currentPeakIndex = -1;
                currentPeakValue = double.MinValue;
            }
            else if (wasNegative && nsdf[i] >= 0)
            {
                wasNegative = false;
            }

            if (!wasNegative && nsdf[i] > currentPeakValue)
            {
                currentPeakIndex = i;
                currentPeakValue = nsdf[i];
            }
        }

        // Add last peak if valid
        if (currentPeakIndex > 0 && currentPeakValue > _smallCutoff)
        {
            peaks.Add((currentPeakIndex, currentPeakValue));
        }

        if (peaks.Count == 0)
            return (-1, 0);

        // Find the maximum peak value
        double maxPeakValue = peaks.Max(p => p.value);
        double threshold = maxPeakValue * _cutoff;

        // Get all peaks above threshold
        var candidatePeaks = peaks.Where(p => p.value >= threshold).ToList();

        if (candidatePeaks.Count == 0)
        {
            return peaks.OrderByDescending(p => p.value).First();
        }

        if (candidatePeaks.Count == 1)
        {
            return candidatePeaks[0];
        }

        // For guitar tuning: the fundamental creates the peak at the longest lag.
        // Harmonics create peaks at shorter lags (higher frequencies).
        // Choose the longest lag (lowest frequency) among strong peaks.
        // This is the most likely fundamental for real instrument signals.
        var longestLagPeak = candidatePeaks.OrderByDescending(p => p.index).First();

        // Verify this isn't a spurious subharmonic by checking if there's
        // a strong peak at a much shorter lag (potential true fundamental)
        var shortestLagPeak = candidatePeaks.OrderBy(p => p.index).First();

        double lagRatio = (double)longestLagPeak.index / shortestLagPeak.index;

        // If the longest lag is 2x-4x the shortest and shortest is quite strong,
        // prefer the shortest (it's probably the true fundamental, longest is subharmonic)
        if (lagRatio > 1.9 && shortestLagPeak.value >= maxPeakValue * 0.85)
        {
            return shortestLagPeak;
        }

        // Otherwise prefer longest lag (fundamental for real guitar)
        return longestLagPeak;
    }

    private static double ParabolicInterpolation(double[] data, int index)
    {
        if (index <= 0 || index >= data.Length - 1)
            return index;

        double y0 = data[index - 1];
        double y1 = data[index];
        double y2 = data[index + 1];

        double denominator = y0 - 2.0 * y1 + y2;
        if (Math.Abs(denominator) < 1e-10)
            return index;

        double d = (y0 - y2) / (2.0 * denominator);
        return index + Math.Clamp(d, -0.5, 0.5);
    }
}
