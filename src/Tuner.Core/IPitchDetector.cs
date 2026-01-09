namespace Tuner.Core;

/// <summary>
/// Interface for pitch detection algorithms.
/// </summary>
public interface IPitchDetector
{
    /// <summary>
    /// Detects the pitch in the given audio samples.
    /// </summary>
    /// <param name="samples">Audio samples (mono, normalized to -1.0 to 1.0).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <returns>Pitch detection result.</returns>
    PitchDetectionResult DetectPitch(ReadOnlySpan<float> samples, int sampleRate);
}
