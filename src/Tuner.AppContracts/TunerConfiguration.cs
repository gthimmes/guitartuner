namespace Tuner.AppContracts;

/// <summary>
/// Configuration options for the tuner engine.
/// </summary>
public sealed class TunerConfiguration
{
    /// <summary>
    /// Gets or sets the tolerance in cents for "in tune" state. Default: 5 cents.
    /// </summary>
    public double InTuneTolerance { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets the number of consecutive frames required for InTune state. Default: 8.
    /// </summary>
    public int StabilityFrames { get; set; } = 8;

    /// <summary>
    /// Gets or sets the EMA smoothing factor (0-1). Higher = more responsive. Default: 0.3.
    /// </summary>
    public double SmoothingAlpha { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the number of raw readings the running median spans before
    /// smoothing. Rejects single-frame outliers (octave errors). Default: 5.
    /// </summary>
    public int MedianWindow { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many frames to ignore after a pluck is detected, so the
    /// inharmonic attack transient never reaches the display. Default: 6.
    /// </summary>
    public int OnsetHoldFrames { get; set; } = 6;

    /// <summary>
    /// Gets or sets the RMS ratio over the running level envelope that counts as
    /// a new pluck (onset). Default: 2.5.
    /// </summary>
    public double OnsetRatio { get; set; } = 2.5;

    /// <summary>
    /// Gets or sets the size of a step (in cents) from the current smoothed
    /// reading that is treated as a jump and must persist before it is shown.
    /// Default: 30.
    /// </summary>
    public double JumpThresholdCents { get; set; } = 30.0;

    /// <summary>
    /// Gets or sets how many consecutive frames a jump must persist before the
    /// reading snaps to it. Default: 4.
    /// </summary>
    public int JumpConfirmFrames { get; set; } = 4;

    /// <summary>
    /// Gets or sets how much louder than the learned background-noise level a
    /// sound must be before the tuner reacts to it. Default: 3 (~10 dB).
    /// </summary>
    public double NoiseGateRatio { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets how closely (in cents) the readings in the median window
    /// must agree before a pitch is believed. Noise that sneaks past the
    /// confidence check reads differently every frame and fails this. Default: 15.
    /// </summary>
    public double MaxSpreadCents { get; set; } = 15.0;

    /// <summary>
    /// Gets or sets the minimum pitch detection confidence (NSDF clarity, 0-1).
    /// A clean plucked note reads 0.95+; room noise mostly below 0.8. Default: 0.8.
    /// </summary>
    public double MinConfidence { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the minimum RMS signal level. Default: 0.002 (very low for sensitivity).
    /// </summary>
    public double MinRmsThreshold { get; set; } = 0.002;

    /// <summary>
    /// Gets or sets the number of frames before allowing string switch (hysteresis). Default: 5.
    /// </summary>
    public int HysteresisFrames { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum cents offset for string matching. Default: 250 (~2.5 semitones).
    /// </summary>
    public double MaxCentsForStringMatch { get; set; } = 250.0;

    /// <summary>
    /// Gets or sets the FFT window size in samples. Default: 4096.
    /// </summary>
    public int WindowSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the hop size in samples. Default: 512.
    /// </summary>
    public int HopSize { get; set; } = 512;

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static TunerConfiguration Default => new();

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (InTuneTolerance <= 0)
            throw new ArgumentException("InTuneTolerance must be positive.", nameof(InTuneTolerance));
        if (StabilityFrames < 1)
            throw new ArgumentException("StabilityFrames must be at least 1.", nameof(StabilityFrames));
        if (SmoothingAlpha is <= 0 or > 1)
            throw new ArgumentException("SmoothingAlpha must be between 0 and 1.", nameof(SmoothingAlpha));
        if (MedianWindow < 1)
            throw new ArgumentException("MedianWindow must be at least 1.", nameof(MedianWindow));
        if (OnsetHoldFrames < 0)
            throw new ArgumentException("OnsetHoldFrames must be non-negative.", nameof(OnsetHoldFrames));
        if (OnsetRatio < 1)
            throw new ArgumentException("OnsetRatio must be at least 1.", nameof(OnsetRatio));
        if (JumpThresholdCents < 0)
            throw new ArgumentException("JumpThresholdCents must be non-negative.", nameof(JumpThresholdCents));
        if (JumpConfirmFrames < 1)
            throw new ArgumentException("JumpConfirmFrames must be at least 1.", nameof(JumpConfirmFrames));
        if (NoiseGateRatio < 1)
            throw new ArgumentException("NoiseGateRatio must be at least 1.", nameof(NoiseGateRatio));
        if (MaxSpreadCents < 0)
            throw new ArgumentException("MaxSpreadCents must be non-negative.", nameof(MaxSpreadCents));
        if (MinConfidence is < 0 or > 1)
            throw new ArgumentException("MinConfidence must be between 0 and 1.", nameof(MinConfidence));
        if (MinRmsThreshold < 0)
            throw new ArgumentException("MinRmsThreshold must be non-negative.", nameof(MinRmsThreshold));
        if (HysteresisFrames < 0)
            throw new ArgumentException("HysteresisFrames must be non-negative.", nameof(HysteresisFrames));
        if (MaxCentsForStringMatch <= 0)
            throw new ArgumentException("MaxCentsForStringMatch must be positive.", nameof(MaxCentsForStringMatch));
        if (WindowSize < 256)
            throw new ArgumentException("WindowSize must be at least 256.", nameof(WindowSize));
        if (HopSize < 1)
            throw new ArgumentException("HopSize must be at least 1.", nameof(HopSize));
    }
}
