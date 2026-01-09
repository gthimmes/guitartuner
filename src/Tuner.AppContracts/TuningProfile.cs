namespace Tuner.AppContracts;

/// <summary>
/// Represents a tuning profile containing target strings.
/// </summary>
public sealed class TuningProfile
{
    /// <summary>
    /// Gets the name of the tuning profile (e.g., "Standard", "Drop D").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the target strings in this tuning, ordered by string number.
    /// </summary>
    public IReadOnlyList<StringTarget> Strings { get; }

    /// <summary>
    /// Gets the tolerance in cents for considering a note "in tune".
    /// </summary>
    public double InTuneTolerance { get; }

    public TuningProfile(string name, IEnumerable<StringTarget> strings, double inTuneTolerance = 5.0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        // Order by string number descending so low E (string 6) appears first/left in UI
        Strings = strings?.OrderByDescending(s => s.StringNumber).ToList().AsReadOnly()
            ?? throw new ArgumentNullException(nameof(strings));
        InTuneTolerance = inTuneTolerance > 0 ? inTuneTolerance : throw new ArgumentOutOfRangeException(nameof(inTuneTolerance));

        if (Strings.Count == 0)
            throw new ArgumentException("Tuning profile must have at least one string.", nameof(strings));
    }

    public override string ToString() => Name;
}
