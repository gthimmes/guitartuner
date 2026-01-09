namespace Tuner.Core;

/// <summary>
/// Utility class for cents calculations.
/// </summary>
public static class CentsCalculator
{
    private const double CentsPerOctave = 1200.0;
    private const double Log2 = 0.6931471805599453; // Math.Log(2)

    /// <summary>
    /// Calculates the cents difference between two frequencies.
    /// </summary>
    /// <param name="detectedFrequency">The detected frequency in Hz.</param>
    /// <param name="targetFrequency">The target frequency in Hz.</param>
    /// <returns>Cents offset. Negative = flat, Positive = sharp.</returns>
    public static double CalculateCents(double detectedFrequency, double targetFrequency)
    {
        if (detectedFrequency <= 0 || targetFrequency <= 0)
            return 0;

        return CentsPerOctave * Math.Log(detectedFrequency / targetFrequency) / Log2;
    }

    /// <summary>
    /// Calculates frequency from a base frequency and cents offset.
    /// </summary>
    public static double CentsToFrequency(double baseFrequency, double cents)
    {
        return baseFrequency * Math.Pow(2.0, cents / CentsPerOctave);
    }

    /// <summary>
    /// Gets the note name for a frequency (e.g., "A4", "E2").
    /// </summary>
    public static string FrequencyToNoteName(double frequency)
    {
        if (frequency <= 0) return "?";

        // A4 = 440 Hz is MIDI note 69
        const double a4 = 440.0;
        const int a4MidiNote = 69;

        double semitones = 12.0 * Math.Log(frequency / a4) / Log2;
        int midiNote = (int)Math.Round(a4MidiNote + semitones);

        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int noteIndex = ((midiNote % 12) + 12) % 12;
        int octave = (midiNote / 12) - 1;

        return $"{noteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Gets the nearest standard frequency for a given frequency.
    /// </summary>
    public static double GetNearestStandardFrequency(double frequency)
    {
        if (frequency <= 0) return 0;

        const double a4 = 440.0;
        double semitones = 12.0 * Math.Log(frequency / a4) / Log2;
        int nearestSemitone = (int)Math.Round(semitones);
        return a4 * Math.Pow(2.0, nearestSemitone / 12.0);
    }
}
