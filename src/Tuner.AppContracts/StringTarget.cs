namespace Tuner.AppContracts;

/// <summary>
/// Represents a target string with its name and frequency.
/// </summary>
public sealed class StringTarget
{
    /// <summary>
    /// Gets the name of the string (e.g., "E2", "A2", "D3").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the target frequency in Hz.
    /// </summary>
    public double Frequency { get; }

    /// <summary>
    /// Gets the string number (1-6 for standard guitar, where 1 is highest pitch).
    /// </summary>
    public int StringNumber { get; }

    public StringTarget(string name, double frequency, int stringNumber)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Frequency = frequency > 0 ? frequency : throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive.");
        StringNumber = stringNumber > 0 ? stringNumber : throw new ArgumentOutOfRangeException(nameof(stringNumber), "String number must be positive.");
    }

    public override string ToString() => $"{Name} ({Frequency:F2} Hz)";

    public override bool Equals(object? obj) =>
        obj is StringTarget other &&
        Name == other.Name &&
        Math.Abs(Frequency - other.Frequency) < 0.01 &&
        StringNumber == other.StringNumber;

    public override int GetHashCode() => HashCode.Combine(Name, Frequency, StringNumber);
}
