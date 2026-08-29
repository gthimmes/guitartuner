using Tuner.AppContracts;

namespace Tuner.Core;

/// <summary>
/// Filters and smooths tuner output for stability.
/// </summary>
/// <remarks>
/// A raw pitch stream from a microphone is noisy in several specific ways, and
/// each stage here targets one of them:
/// <list type="bullet">
///   <item><b>Room noise.</b> Fans, voices, keyboard clatter. An adaptive
///   <i>noise gate</i> learns the level of whatever non-tonal sound is in the
///   room and ignores anything that isn't clearly louder than it, so the tuner
///   only reacts to a played note.</item>
///   <item><b>Non-notes that pass the confidence check.</b> Noise sometimes
///   produces a confident-looking pitch for a frame or two, but a different one
///   every frame. An <i>agreement check</i> requires the recent readings to
///   agree with each other before any of them is believed.</item>
///   <item><b>Attack transient.</b> The first ~50-100 ms after a pluck is
///   inharmonic and reads sharp (string tension spikes) before settling. An
///   <i>onset hold</i> detects the sudden RMS jump, throws away whatever the
///   filter had, and holds the display until the note settles.</item>
///   <item><b>Single-frame outliers</b> (octave errors, a harmonic winning the
///   NSDF peak for one frame). A running <i>median</i> discards these before
///   they ever reach the smoother.</item>
///   <item><b>Genuine jumps</b> (a different string, or the peg turned a lot).
///   A <i>jump gate</i> ignores a large step until it has persisted for a few
///   frames, then snaps to it instead of slewing through every value between.</item>
/// </list>
/// What survives is exponentially smoothed for a steady needle.
/// </remarks>
public sealed class StabilityFilter
{
    private readonly double _smoothingAlpha;
    private readonly int _stabilityFrames;
    private readonly double _inTuneTolerance;
    private readonly int _medianWindow;
    private readonly int _onsetHoldFrames;
    private readonly double _onsetRatio;
    private readonly double _jumpThresholdCents;
    private readonly int _jumpConfirmFrames;
    private readonly double _noiseGateRatio;
    private readonly double _maxSpreadCents;

    private const double EnvelopeAlpha = 0.15;
    /// <summary>How quickly the noise floor rises toward louder non-tonal sound (~0.5 s time constant at 100 fps).</summary>
    private const double NoiseFloorRiseAlpha = 0.02;
    /// <summary>How quickly the noise floor drops when the room gets quieter.</summary>
    private const double NoiseFloorFallAlpha = 0.3;

    private readonly Queue<(double Frequency, double Cents)> _history = new();

    private double _smoothedFrequency;
    private double _smoothedCents;
    private int _consecutiveInTuneCount;
    private TunerState _previousState;
    private bool _hasData;

    private double _signalEnvelope;
    private bool _hasEnvelope;
    private double _noiseFloor;
    private bool _hasNoiseFloor;
    private int _onsetHoldRemaining;
    private int _jumpCount;
    private bool _snapNext;

    public StabilityFilter(
        double smoothingAlpha = 0.3,
        int stabilityFrames = 8,
        double inTuneTolerance = 5.0,
        int medianWindow = 5,
        int onsetHoldFrames = 6,
        double jumpThresholdCents = 30.0,
        int jumpConfirmFrames = 4,
        double onsetRatio = 2.5,
        double noiseGateRatio = 3.0,
        double maxSpreadCents = 15.0)
    {
        _smoothingAlpha = Math.Clamp(smoothingAlpha, 0.01, 1.0);
        _stabilityFrames = Math.Max(1, stabilityFrames);
        _inTuneTolerance = Math.Max(0.1, inTuneTolerance);
        _medianWindow = Math.Max(1, medianWindow);
        _onsetHoldFrames = Math.Max(0, onsetHoldFrames);
        _jumpThresholdCents = Math.Max(0, jumpThresholdCents);
        _jumpConfirmFrames = Math.Max(1, jumpConfirmFrames);
        _onsetRatio = Math.Max(1.0, onsetRatio);
        _noiseGateRatio = Math.Max(1.0, noiseGateRatio);
        _maxSpreadCents = Math.Max(0, maxSpreadCents);
    }

    /// <summary>
    /// Gets the smoothed frequency.
    /// </summary>
    public double SmoothedFrequency => _smoothedFrequency;

    /// <summary>
    /// Gets the smoothed cents offset.
    /// </summary>
    public double SmoothedCents => _smoothedCents;

    /// <summary>
    /// Gets the learned RMS level of non-tonal background sound (0 until any has been heard).
    /// </summary>
    public double NoiseFloor => _hasNoiseFloor ? _noiseFloor : 0;

    /// <summary>
    /// Processes a raw measurement and returns a filtered result.
    /// </summary>
    public FilteredResult Process(double frequency, double cents, double confidence, double signalLevel, double minConfidence, double minRms)
    {
        bool tonal = confidence >= minConfidence && frequency > 0;
        bool onset = _hasEnvelope && signalLevel > _signalEnvelope * _onsetRatio;

        // Noise gate: the signal must clear both the absolute floor and a
        // margin over whatever background sound the room has. This is what
        // stops the display reacting to every noise in the room. The gate is
        // taken from the floor as learned *before* this frame, then the frame
        // teaches the floor for the next one.
        double gate = Math.Max(minRms, _hasNoiseFloor ? _noiseFloor * _noiseGateRatio : 0);

        // A pluck's attack is loud and non-tonal for a frame or two; it must
        // not teach the noise gate that the room got louder.
        UpdateNoiseFloor(signalLevel, tonal, burst: onset || _onsetHoldRemaining > 0);

        if (signalLevel < gate)
        {
            UpdateEnvelope(signalLevel);
            ClearPitchHistory();
            _hasData = false;
            _previousState = TunerState.TooQuiet;
            return new FilteredResult
            {
                Frequency = 0,
                Cents = 0,
                State = TunerState.TooQuiet
            };
        }

        // Onset detection: a sudden jump in level over the running envelope is a
        // new pluck. Whatever was in the pipeline belongs to the previous note
        // (or to the attack transient), so throw it away and hold briefly.
        if (onset)
        {
            ClearPitchHistory();
            _onsetHoldRemaining = _onsetHoldFrames;
            _snapNext = true;
            // Snap the envelope to the new level so the frames that follow the
            // attack are judged against it, not against the old quiet level
            // (which would re-trigger the onset every frame until it caught up).
            _signalEnvelope = signalLevel;
        }
        UpdateEnvelope(signalLevel);

        // Not a clear periodic signal: nothing to measure this frame.
        if (!tonal)
        {
            _consecutiveInTuneCount = 0;
            return new FilteredResult
            {
                Frequency = _hasData ? _smoothedFrequency : 0,
                Cents = _hasData ? _smoothedCents : 0,
                State = NoPitchState()
            };
        }

        // Still inside the attack of a fresh pluck: those readings are not
        // representative of the settled pitch, so don't even collect them.
        if (_onsetHoldRemaining > 0)
        {
            _onsetHoldRemaining--;
            return HoldResult();
        }

        // Median pre-filter: rejects single-frame outliers (octave errors etc.)
        _history.Enqueue((frequency, cents));
        while (_history.Count > _medianWindow)
            _history.Dequeue();

        // Need a full window of readings before the median means anything.
        if (_history.Count < _medianWindow)
            return HoldResult();

        var (medianFrequency, medianCents) = Median(_history);

        // Agreement check: a real note gives nearly the same reading every
        // frame; noise that happens to pass the confidence test gives a
        // different one each time. Allow a single outlier (the median removes
        // it), but otherwise require the window to agree before believing it.
        if (!ReadingsAgree(medianCents))
            return HoldResult();

        // Jump gate: a large step away from the smoothed value must persist
        // before we believe it. Then snap rather than slew.
        if (_hasData && Math.Abs(medianCents - _smoothedCents) > _jumpThresholdCents)
        {
            _jumpCount++;
            if (_jumpCount < _jumpConfirmFrames)
                return HoldResult();

            _smoothedFrequency = medianFrequency;
            _smoothedCents = medianCents;
            _consecutiveInTuneCount = 0;
        }
        else if (_hasData && !_snapNext)
        {
            _smoothedFrequency = _smoothingAlpha * medianFrequency + (1 - _smoothingAlpha) * _smoothedFrequency;
            _smoothedCents = _smoothingAlpha * medianCents + (1 - _smoothingAlpha) * _smoothedCents;
        }
        else
        {
            // First reading of a note (or first after a re-pluck): start from
            // the median directly instead of slewing from a stale value.
            _smoothedFrequency = medianFrequency;
            _smoothedCents = medianCents;
            _hasData = true;
        }
        _jumpCount = 0;
        _snapNext = false;

        // Determine state
        TunerState state;
        double absCents = Math.Abs(_smoothedCents);

        if (absCents <= _inTuneTolerance)
        {
            _consecutiveInTuneCount++;
            state = _consecutiveInTuneCount >= _stabilityFrames
                ? TunerState.InTune
                : (_smoothedCents < 0 ? TunerState.Flat : TunerState.Sharp);
        }
        else
        {
            _consecutiveInTuneCount = 0;
            state = _smoothedCents < 0 ? TunerState.Flat : TunerState.Sharp;
        }

        _previousState = state;

        return new FilteredResult
        {
            Frequency = _smoothedFrequency,
            Cents = _smoothedCents,
            State = state
        };
    }

    /// <summary>
    /// Resets the filter state.
    /// </summary>
    public void Reset()
    {
        ClearPitchHistory();
        _smoothedFrequency = 0;
        _smoothedCents = 0;
        _previousState = TunerState.Unknown;
        _hasData = false;
        _signalEnvelope = 0;
        _hasEnvelope = false;
        _noiseFloor = 0;
        _hasNoiseFloor = false;
    }

    /// <summary>
    /// While holding (attack, settling, or unconfirmed jump), keep showing the
    /// last good reading if there is one so the needle doesn't twitch;
    /// otherwise report that nothing is locked yet.
    /// </summary>
    private FilteredResult HoldResult()
    {
        bool hadPitch = _hasData && _previousState is TunerState.Flat or TunerState.Sharp or TunerState.InTune;
        return new FilteredResult
        {
            Frequency = _hasData ? _smoothedFrequency : 0,
            Cents = _hasData ? _smoothedCents : 0,
            State = hadPitch ? _previousState : NoPitchState()
        };
    }

    /// <summary>
    /// The state to report when this frame yields no usable pitch: if a note
    /// was already locked we're merely unstable (keep the needle where it is);
    /// if nothing was locked, the sound simply isn't a note yet and the UI
    /// should stay idle rather than react to it.
    /// </summary>
    private TunerState NoPitchState() => _hasData ? TunerState.Unstable : TunerState.Listening;

    private bool ReadingsAgree(double medianCents)
    {
        int n = _history.Count;
        int required = n <= 2 ? n : n - 1;
        int agreeing = 0;
        foreach (var (_, c) in _history)
        {
            if (Math.Abs(c - medianCents) <= _maxSpreadCents)
                agreeing++;
        }
        return agreeing >= required;
    }

    private void ClearPitchHistory()
    {
        _history.Clear();
        _consecutiveInTuneCount = 0;
        _onsetHoldRemaining = 0;
        _jumpCount = 0;
    }

    private void UpdateEnvelope(double signalLevel)
    {
        if (_hasEnvelope)
            _signalEnvelope = EnvelopeAlpha * signalLevel + (1 - EnvelopeAlpha) * _signalEnvelope;
        else
        {
            _signalEnvelope = signalLevel;
            _hasEnvelope = true;
        }
    }

    /// <summary>
    /// Tracks the level of non-tonal background sound. It rises slowly toward
    /// louder noise and falls quickly when the room gets quieter. A tonal frame
    /// (a note) never raises it -- a long sustained note must not be mistaken
    /// for a noisier room -- but can lower it if it's quieter than the floor.
    /// </summary>
    private void UpdateNoiseFloor(double signalLevel, bool tonal, bool burst)
    {
        if (!_hasNoiseFloor)
        {
            if (tonal || burst)
                return;
            _noiseFloor = signalLevel;
            _hasNoiseFloor = true;
            return;
        }

        if (signalLevel < _noiseFloor)
            _noiseFloor += NoiseFloorFallAlpha * (signalLevel - _noiseFloor);
        else if (!tonal && !burst)
            _noiseFloor += NoiseFloorRiseAlpha * (signalLevel - _noiseFloor);
    }

    private static (double Frequency, double Cents) Median(Queue<(double Frequency, double Cents)> history)
    {
        int n = history.Count;
        if (n == 1)
            return history.Peek();

        var freqs = new double[n];
        var cents = new double[n];
        int i = 0;
        foreach (var (f, c) in history)
        {
            freqs[i] = f;
            cents[i] = c;
            i++;
        }
        Array.Sort(freqs);
        Array.Sort(cents);

        int m = n / 2;
        return n % 2 == 1
            ? (freqs[m], cents[m])
            : ((freqs[m - 1] + freqs[m]) / 2, (cents[m - 1] + cents[m]) / 2);
    }
}

/// <summary>
/// Result from stability filter.
/// </summary>
public readonly struct FilteredResult
{
    public double Frequency { get; init; }
    public double Cents { get; init; }
    public TunerState State { get; init; }
}
