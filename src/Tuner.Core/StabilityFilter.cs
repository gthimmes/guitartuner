using Tuner.AppContracts;

namespace Tuner.Core;

/// <summary>
/// Filters and smooths tuner output for stability.
/// </summary>
public sealed class StabilityFilter
{
    private readonly double _smoothingAlpha;
    private readonly int _stabilityFrames;
    private readonly double _inTuneTolerance;

    private double _smoothedFrequency;
    private double _smoothedCents;
    private int _consecutiveInTuneCount;
    private TunerState _previousState;
    private bool _hasData;

    public StabilityFilter(double smoothingAlpha = 0.5, int stabilityFrames = 8, double inTuneTolerance = 5.0)
    {
        _smoothingAlpha = Math.Clamp(smoothingAlpha, 0.01, 1.0);
        _stabilityFrames = Math.Max(1, stabilityFrames);
        _inTuneTolerance = Math.Max(0.1, inTuneTolerance);
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
    /// Processes a raw measurement and returns a filtered result.
    /// </summary>
    public FilteredResult Process(double frequency, double cents, double confidence, double signalLevel, double minConfidence, double minRms)
    {
        // Check for no signal - reset smoothing so next note starts fresh
        if (signalLevel < minRms)
        {
            _consecutiveInTuneCount = 0;
            _hasData = false;  // Reset so next valid reading starts fresh
            return new FilteredResult
            {
                Frequency = 0,
                Cents = 0,
                State = TunerState.TooQuiet
            };
        }

        // Check for low confidence
        if (confidence < minConfidence || frequency <= 0)
        {
            _consecutiveInTuneCount = 0;
            return new FilteredResult
            {
                Frequency = _hasData ? _smoothedFrequency : 0,
                Cents = _hasData ? _smoothedCents : 0,
                State = TunerState.Unstable
            };
        }

        // Apply exponential moving average
        if (_hasData)
        {
            _smoothedFrequency = _smoothingAlpha * frequency + (1 - _smoothingAlpha) * _smoothedFrequency;
            _smoothedCents = _smoothingAlpha * cents + (1 - _smoothingAlpha) * _smoothedCents;
        }
        else
        {
            _smoothedFrequency = frequency;
            _smoothedCents = cents;
            _hasData = true;
        }

        // Determine state
        TunerState state;
        double absCents = Math.Abs(_smoothedCents);

        if (absCents <= _inTuneTolerance)
        {
            _consecutiveInTuneCount++;
            state = _consecutiveInTuneCount >= _stabilityFrames ? TunerState.InTune : _previousState;

            // If we haven't reached stability yet, show flat/sharp
            if (state != TunerState.InTune)
            {
                state = _smoothedCents < 0 ? TunerState.Flat : TunerState.Sharp;
            }
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
        _smoothedFrequency = 0;
        _smoothedCents = 0;
        _consecutiveInTuneCount = 0;
        _previousState = TunerState.Unknown;
        _hasData = false;
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
