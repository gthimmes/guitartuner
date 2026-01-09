using Tuner.AppContracts;

namespace Tuner.Core;

/// <summary>
/// Predefined tuning profiles for guitar.
/// String names use simple letters - lowercase 'e' for high E, uppercase 'E' for low E.
/// </summary>
public static class TuningProfiles
{
    /// <summary>
    /// Standard guitar tuning: E, A, D, G, B, e (low to high, as viewed from front)
    /// </summary>
    public static TuningProfile Standard { get; } = new(
        "Standard",
        new[]
        {
            new StringTarget("E", 82.41, 6),    // Low E (leftmost when viewing guitar)
            new StringTarget("A", 110.00, 5),
            new StringTarget("D", 146.83, 4),
            new StringTarget("G", 196.00, 3),
            new StringTarget("B", 246.94, 2),
            new StringTarget("e", 329.63, 1)    // High e (rightmost when viewing guitar)
        }
    );

    /// <summary>
    /// Drop D tuning: D, A, D, G, B, e (low to high, as viewed from front)
    /// </summary>
    public static TuningProfile DropD { get; } = new(
        "Drop D",
        new[]
        {
            new StringTarget("D", 73.42, 6),    // Dropped to D (leftmost)
            new StringTarget("A", 110.00, 5),
            new StringTarget("D", 146.83, 4),
            new StringTarget("G", 196.00, 3),
            new StringTarget("B", 246.94, 2),
            new StringTarget("e", 329.63, 1)    // High e (rightmost)
        }
    );

    /// <summary>
    /// Half-step down: All strings tuned down one semitone
    /// </summary>
    public static TuningProfile HalfStepDown { get; } = new(
        "Half Step Down",
        new[]
        {
            new StringTarget("E", 77.78, 6),
            new StringTarget("A", 103.83, 5),
            new StringTarget("D", 138.59, 4),
            new StringTarget("G", 185.00, 3),
            new StringTarget("B", 233.08, 2),
            new StringTarget("e", 311.13, 1)
        }
    );

    /// <summary>
    /// Open G tuning: D, G, D, G, B, D (low to high, as viewed from front)
    /// </summary>
    public static TuningProfile OpenG { get; } = new(
        "Open G",
        new[]
        {
            new StringTarget("D", 73.42, 6),
            new StringTarget("G", 98.00, 5),
            new StringTarget("D", 146.83, 4),
            new StringTarget("G", 196.00, 3),
            new StringTarget("B", 246.94, 2),
            new StringTarget("D", 293.66, 1)
        }
    );

    /// <summary>
    /// Open D tuning: D, A, D, F#, A, D (low to high, as viewed from front)
    /// </summary>
    public static TuningProfile OpenD { get; } = new(
        "Open D",
        new[]
        {
            new StringTarget("D", 73.42, 6),
            new StringTarget("A", 110.00, 5),
            new StringTarget("D", 146.83, 4),
            new StringTarget("F#", 185.00, 3),
            new StringTarget("A", 220.00, 2),
            new StringTarget("D", 293.66, 1)
        }
    );

    /// <summary>
    /// DADGAD tuning: D, A, D, G, A, D (low to high, as viewed from front)
    /// </summary>
    public static TuningProfile DADGAD { get; } = new(
        "DADGAD",
        new[]
        {
            new StringTarget("D", 73.42, 6),
            new StringTarget("A", 110.00, 5),
            new StringTarget("D", 146.83, 4),
            new StringTarget("G", 196.00, 3),
            new StringTarget("A", 220.00, 2),
            new StringTarget("D", 293.66, 1)
        }
    );

    /// <summary>
    /// All available tuning profiles.
    /// </summary>
    public static IReadOnlyList<TuningProfile> All { get; } = new[]
    {
        Standard,
        DropD,
        HalfStepDown,
        OpenG,
        OpenD,
        DADGAD
    };

    /// <summary>
    /// Creates a custom tuning profile.
    /// </summary>
    public static TuningProfile CreateCustom(string name, params (string name, double frequency, int stringNumber)[] strings)
    {
        return new TuningProfile(
            name,
            strings.Select(s => new StringTarget(s.name, s.frequency, s.stringNumber))
        );
    }
}
