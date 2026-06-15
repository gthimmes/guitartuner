namespace Tuner.Diagnostics;

/// <summary>
/// Generates physically-plausible plucked-guitar tones with a known exact
/// fundamental frequency, so detector accuracy can be measured to the cent.
/// A real recording is never guaranteed in tune; a synthesized tone is.
/// </summary>
public static class ToneGenerator
{
    /// <summary>
    /// Builds a mono plucked-string tone: fundamental + decaying harmonics, an
    /// exponential body decay, and a short attack ramp. Harmonic amplitudes are
    /// chosen so upper partials are strong (the realistic case that causes
    /// octave errors in naive pitch detectors).
    /// </summary>
    public static float[] PluckedString(double fundamentalHz, int sampleRate, double seconds)
    {
        int n = (int)(sampleRate * seconds);
        var buf = new float[n];

        // Relative amplitudes of partials 1..6. Note partial 2 is nearly as loud
        // as the fundamental — typical for a steel string and a classic octave trap.
        double[] partials = { 1.0, 0.85, 0.55, 0.35, 0.20, 0.12 };
        // Higher partials decay faster than the fundamental (string damping).
        double[] decay = { 1.2, 1.8, 2.6, 3.5, 4.5, 6.0 };

        double attack = 0.006; // 6 ms attack
        int attackSamples = Math.Max(1, (int)(attack * sampleRate));

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sampleRate;
            double sample = 0;
            for (int h = 0; h < partials.Length; h++)
            {
                double freq = fundamentalHz * (h + 1);
                if (freq >= sampleRate / 2.0) break; // anti-alias guard
                double env = Math.Exp(-decay[h] * t);
                sample += partials[h] * env * Math.Sin(2 * Math.PI * freq * t);
            }

            double attackGain = i < attackSamples ? (double)i / attackSamples : 1.0;
            buf[i] = (float)(0.25 * attackGain * sample);
        }

        return buf;
    }
}
