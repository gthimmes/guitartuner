namespace Tuner.Audio.Abstractions;

/// <summary>
/// Plays a sustained reference tone at a given pitch through the default audio
/// output, so a string can be tuned by ear.
/// </summary>
public interface IReferenceTonePlayer : IDisposable
{
    /// <summary>
    /// Gets whether a tone is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Gets the frequency of the tone being played, or null when silent.
    /// </summary>
    double? Frequency { get; }

    /// <summary>
    /// Starts playing a tone at the given frequency, or changes the pitch of
    /// the tone already playing.
    /// </summary>
    void Play(double frequency);

    /// <summary>
    /// Stops the tone.
    /// </summary>
    void Stop();
}
