namespace Tuner.Core;

/// <summary>
/// Generates a sustained, guitar-like reference tone at an exact frequency so
/// the player can tune by ear (match the string until the beating stops).
/// </summary>
/// <remarks>
/// The tone is additive: the fundamental plus five harmonics with amplitudes
/// falling off roughly like a plucked string. The harmonics matter -- a bare
/// 82 Hz sine is almost inaudible on laptop speakers, and the ear locates the
/// pitch from the harmonic series anyway. Start/stop are ramped over 20 ms so
/// there are no clicks. Platform-independent; an audio output wraps
/// <see cref="Read"/> as its sample source.
/// </remarks>
public sealed class ReferenceToneSynthesizer
{
    /// <summary>Relative strength of harmonics 1..6.</summary>
    private static readonly double[] HarmonicGains = { 1.0, 0.6, 0.4, 0.25, 0.15, 0.08 };

    /// <summary>Peak output level (full scale = 1.0). Comfortable, not startling.</summary>
    private const double OutputLevel = 0.35;
    private const double RampSeconds = 0.02;

    private readonly int _sampleRate;
    private readonly double _rampStep;
    private readonly double _normalization;

    private double _phase;      // fundamental phase, in cycles [0, 1)
    private double _gain;       // current envelope, 0..1
    private volatile bool _on;  // target: fading in (true) or out (false)
    private double _frequency = 110.0;

    public ReferenceToneSynthesizer(int sampleRate = 44100)
    {
        if (sampleRate < 8000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));

        _sampleRate = sampleRate;
        _rampStep = 1.0 / (RampSeconds * sampleRate);
        _normalization = OutputLevel / HarmonicGains.Sum();
    }

    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets or sets the fundamental frequency in Hz. May be changed while
    /// playing; the phase is continuous so there is no click.
    /// </summary>
    public double Frequency
    {
        get => _frequency;
        set
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Frequency must be positive.");
            _frequency = value;
        }
    }

    /// <summary>Whether the tone is on (or fading in).</summary>
    public bool IsOn => _on;

    /// <summary>Whether the output has fully faded to silence.</summary>
    public bool IsSilent => !_on && _gain <= 0;

    public void Start() => _on = true;

    public void Stop() => _on = false;

    /// <summary>
    /// Fills <paramref name="count"/> mono float samples into the buffer.
    /// Always produces <paramref name="count"/> samples (silence when off).
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        double target = _on ? 1.0 : 0.0;
        double increment = _frequency / _sampleRate;

        for (int i = 0; i < count; i++)
        {
            if (_gain < target)
                _gain = Math.Min(target, _gain + _rampStep);
            else if (_gain > target)
                _gain = Math.Max(target, _gain - _rampStep);

            if (_gain <= 0)
            {
                buffer[offset + i] = 0f;
                _phase = 0;
                continue;
            }

            double angle = 2 * Math.PI * _phase;
            double sample = 0;
            for (int h = 0; h < HarmonicGains.Length; h++)
                sample += HarmonicGains[h] * Math.Sin((h + 1) * angle);

            buffer[offset + i] = (float)(sample * _normalization * _gain);

            _phase += increment;
            if (_phase >= 1.0)
                _phase -= 1.0;
        }

        return count;
    }
}
