namespace Tuner.Audio.Abstractions;

/// <summary>
/// Event arguments containing audio sample data.
/// </summary>
public sealed class AudioFrameEventArgs : EventArgs
{
    /// <summary>
    /// Gets the audio samples as float values (-1.0 to 1.0).
    /// </summary>
    public float[] Samples { get; }

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the timestamp when these samples were captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    public AudioFrameEventArgs(float[] samples, int sampleRate, int channels)
    {
        Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        SampleRate = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));
        Channels = channels > 0 ? channels : throw new ArgumentOutOfRangeException(nameof(channels));
        Timestamp = DateTimeOffset.Now;
    }
}
