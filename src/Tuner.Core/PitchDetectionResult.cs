namespace Tuner.Core;

/// <summary>
/// Result of pitch detection.
/// </summary>
public readonly struct PitchDetectionResult
{
    /// <summary>
    /// Gets the detected frequency in Hz. 0 if no pitch detected.
    /// </summary>
    public double Frequency { get; init; }

    /// <summary>
    /// Gets the confidence level (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets whether a valid pitch was detected.
    /// </summary>
    public bool IsValid => Frequency > 0 && Confidence > 0;

    /// <summary>
    /// Empty result indicating no pitch detected.
    /// </summary>
    public static PitchDetectionResult Empty => new() { Frequency = 0, Confidence = 0 };

    public override string ToString() =>
        IsValid ? $"{Frequency:F2} Hz ({Confidence:P0})" : "No pitch";
}
