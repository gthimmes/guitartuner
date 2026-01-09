namespace Tuner.AppContracts;

/// <summary>
/// Represents the current state of the tuner.
/// </summary>
public enum TunerState
{
    /// <summary>
    /// Initial state or unable to determine state.
    /// </summary>
    Unknown,

    /// <summary>
    /// Listening for audio input but no signal detected yet.
    /// </summary>
    Listening,

    /// <summary>
    /// Signal level is too low to detect pitch accurately.
    /// </summary>
    TooQuiet,

    /// <summary>
    /// Signal detected but pitch is unstable or confidence is low.
    /// </summary>
    Unstable,

    /// <summary>
    /// Detected pitch is below the target frequency (tune up).
    /// </summary>
    Flat,

    /// <summary>
    /// Detected pitch is above the target frequency (tune down).
    /// </summary>
    Sharp,

    /// <summary>
    /// Detected pitch is within tolerance of target frequency.
    /// </summary>
    InTune
}
