namespace Tuner.AppContracts;

/// <summary>
/// Represents a single frame of tuner output data.
/// </summary>
public sealed class TunerFrame
{
    /// <summary>
    /// Gets the timestamp when this frame was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the detected frequency in Hz. Null if no pitch detected.
    /// </summary>
    public double? DetectedFrequency { get; init; }

    /// <summary>
    /// Gets the confidence level of the pitch detection (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the detected note name (e.g., "A4", "E2"). Null if no pitch detected.
    /// </summary>
    public string? DetectedNoteName { get; init; }

    /// <summary>
    /// Gets the cents offset from the nearest target string.
    /// Negative = flat, Positive = sharp.
    /// </summary>
    public double CentsOffset { get; init; }

    /// <summary>
    /// Gets the target string being tuned. Null if no string matched.
    /// </summary>
    public StringTarget? TargetString { get; init; }

    /// <summary>
    /// Gets the current tuner state.
    /// </summary>
    public TunerState State { get; init; }

    /// <summary>
    /// Gets the RMS signal level (0.0 to 1.0).
    /// </summary>
    public double SignalLevel { get; init; }

    /// <summary>
    /// Creates an empty frame with default values.
    /// </summary>
    public static TunerFrame Empty => new()
    {
        Timestamp = DateTimeOffset.Now,
        DetectedFrequency = null,
        Confidence = 0,
        DetectedNoteName = null,
        CentsOffset = 0,
        TargetString = null,
        State = TunerState.Unknown,
        SignalLevel = 0
    };

    public override string ToString()
    {
        if (State == TunerState.Unknown || State == TunerState.Listening || State == TunerState.TooQuiet)
            return $"[{State}]";

        return $"[{State}] {TargetString?.Name ?? "?"} {CentsOffset:+0.0;-0.0;0} cents ({DetectedFrequency:F1} Hz, {Confidence:P0})";
    }
}
