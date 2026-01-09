using Tuner.AppContracts;

namespace Tuner.Core;

/// <summary>
/// Classifies which string is being played based on detected frequency.
/// </summary>
public sealed class StringClassifier
{
    private readonly double _maxCentsForMatch;
    private readonly int _hysteresisFrames;

    private StringTarget? _currentString;
    private StringTarget? _candidateString;
    private int _candidateCount;

    public StringClassifier(double maxCentsForMatch = 250.0, int hysteresisFrames = 5)
    {
        _maxCentsForMatch = maxCentsForMatch;
        _hysteresisFrames = hysteresisFrames;
    }

    /// <summary>
    /// Gets the currently locked string (with hysteresis applied).
    /// </summary>
    public StringTarget? CurrentString => _currentString;

    /// <summary>
    /// Classifies the detected frequency to a string in the tuning profile.
    /// </summary>
    /// <param name="frequency">Detected frequency in Hz.</param>
    /// <param name="profile">Current tuning profile.</param>
    /// <returns>Classification result with string and cents offset.</returns>
    public StringClassificationResult Classify(double frequency, TuningProfile profile)
    {
        if (frequency <= 0 || profile.Strings.Count == 0)
        {
            // No valid frequency - reset candidate but keep current string briefly
            _candidateString = null;
            _candidateCount = 0;
            return StringClassificationResult.Empty;
        }

        // Find closest string
        StringTarget? closestString = null;
        double minCentsAbs = double.MaxValue;
        double closestCents = 0;

        foreach (var target in profile.Strings)
        {
            double cents = CentsCalculator.CalculateCents(frequency, target.Frequency);
            double centsAbs = Math.Abs(cents);

            if (centsAbs < minCentsAbs)
            {
                minCentsAbs = centsAbs;
                closestCents = cents;
                closestString = target;
            }
        }

        // Check if within acceptable range
        if (closestString == null || minCentsAbs > _maxCentsForMatch)
        {
            _candidateString = null;
            _candidateCount = 0;
            return StringClassificationResult.Empty;
        }

        // If no current string, immediately adopt the closest
        if (_currentString == null)
        {
            _currentString = closestString;
            _candidateString = null;
            _candidateCount = 0;
            return new StringClassificationResult
            {
                TargetString = closestString,
                CentsOffset = closestCents
            };
        }

        // If same as current string, use it and reset candidate
        if (closestString.Name == _currentString.Name)
        {
            _candidateString = null;
            _candidateCount = 0;
            return new StringClassificationResult
            {
                TargetString = _currentString,
                CentsOffset = closestCents
            };
        }

        // Different string detected - apply hysteresis
        if (_candidateString != null && closestString.Name == _candidateString.Name)
        {
            // Same candidate as before, increment count
            _candidateCount++;
        }
        else
        {
            // New candidate, start counting
            _candidateString = closestString;
            _candidateCount = 1;
        }

        // Check if we should switch
        if (_candidateCount >= _hysteresisFrames)
        {
            _currentString = _candidateString;
            _candidateString = null;
            _candidateCount = 0;
            return new StringClassificationResult
            {
                TargetString = _currentString,
                CentsOffset = closestCents
            };
        }

        // Keep current string but calculate cents from actual frequency to current target
        double currentCents = CentsCalculator.CalculateCents(frequency, _currentString.Frequency);
        return new StringClassificationResult
        {
            TargetString = _currentString,
            CentsOffset = currentCents
        };
    }

    /// <summary>
    /// Resets the classifier state.
    /// </summary>
    public void Reset()
    {
        _currentString = null;
        _candidateString = null;
        _candidateCount = 0;
    }
}

/// <summary>
/// Result of string classification.
/// </summary>
public readonly struct StringClassificationResult
{
    /// <summary>
    /// The matched string target.
    /// </summary>
    public StringTarget? TargetString { get; init; }

    /// <summary>
    /// Cents offset from target. Negative = flat, Positive = sharp.
    /// </summary>
    public double CentsOffset { get; init; }

    /// <summary>
    /// Whether a valid classification was made.
    /// </summary>
    public bool IsValid => TargetString != null;

    /// <summary>
    /// Empty result.
    /// </summary>
    public static StringClassificationResult Empty => new() { TargetString = null, CentsOffset = 0 };
}
