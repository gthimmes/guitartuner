using Tuner.Core;
using Tuner.Diagnostics;

// Standard tuning, exact equal-tempered frequencies (A4 = 440).
var standard = new (string Name, double Hz)[]
{
    ("E2", 82.4069),
    ("A2", 110.0000),
    ("D3", 146.8324),
    ("G3", 195.9977),
    ("B3", 246.9417),
    ("E4", 329.6276),
};

const int WindowSize = 4096;
const int HopSize = 512;
const double MinFreq = 70, MaxFreq = 400;
const double MinConfidence = 0.7;

if (args.Length == 0)
{
    Console.WriteLine("usage: gen <outdir> | analyze <wav> [expectedHz] | analyze-all <dir>");
    return 0;
}

switch (args[0])
{
    case "gen":
        Gen(args[1]);
        break;
    case "analyze":
        AnalyzeFile(args[1], args.Length > 2 ? double.Parse(args[2]) : (double?)null);
        break;
    case "analyze-all":
        foreach (var wav in Directory.GetFiles(args[1], "*.wav").OrderBy(f => f))
            AnalyzeFile(wav, null);
        break;
    default:
        Console.WriteLine($"unknown command: {args[0]}");
        return 1;
}
return 0;

void Gen(string outDir)
{
    Directory.CreateDirectory(outDir);
    foreach (var (name, hz) in standard)
    {
        var samples = ToneGenerator.PluckedString(hz, 48000, 3.0);
        string path = Path.Combine(outDir, $"synth_{name}_{hz:F2}Hz_48k.wav");
        WavIo.WriteFloatMono(path, samples, 48000);
        Console.WriteLine($"wrote {path}");
    }
    // One 44.1 kHz variant to exercise a non-48k sample rate through the pipeline.
    var e2 = ToneGenerator.PluckedString(82.4069, 44100, 3.0);
    string p44 = Path.Combine(outDir, "synth_E2_82.41Hz_44k.wav");
    WavIo.WriteFloatMono(p44, e2, 44100);
    Console.WriteLine($"wrote {p44}");

    // Noisy variants of the two E strings to stress octave-error robustness
    // (realistic mic conditions: hiss, hum, low SNR). -20 dB white noise.
    foreach (var (name, hz) in new[] { ("E2", 82.4069), ("E4", 329.6276) })
    {
        var clean = ToneGenerator.PluckedString(hz, 48000, 3.0);
        var noisy = AddNoise(clean, 0.10, seed: name.Length * 7 + 1);
        string np = Path.Combine(outDir, $"synthnoisy_{name}_{hz:F2}Hz_48k.wav");
        WavIo.WriteFloatMono(np, noisy, 48000);
        Console.WriteLine($"wrote {np}");
    }
}

float[] AddNoise(float[] input, double amplitude, int seed)
{
    var rng = new Random(seed);
    var outp = new float[input.Length];
    for (int i = 0; i < input.Length; i++)
        outp[i] = (float)(input[i] + amplitude * (rng.NextDouble() * 2 - 1));
    return outp;
}

void AnalyzeFile(string path, double? expectedHz)
{
    var wav = WavIo.Read(path);
    expectedHz ??= GuessExpectedFromName(Path.GetFileNameWithoutExtension(path));

    var withHann = Run(wav.Samples, wav.SampleRate, applyHann: true);
    var noHann = Run(wav.Samples, wav.SampleRate, applyHann: false);

    Console.WriteLine($"\n=== {Path.GetFileName(path)}  ({wav.SampleRate} Hz, {wav.Samples.Length} samples, {wav.Samples.Length / (double)wav.SampleRate:F2}s) ===");
    if (expectedHz is double exp)
    {
        var (n, c) = NearestString(exp);
        Console.WriteLine($"  expected: {exp:F2} Hz  ({n})");
    }
    Report("  with Hann (OLD buggy path) ", withHann, expectedHz);
    Report("  no  Hann (current engine)  ", noHann, expectedHz);
}

Stats Run(float[] samples, int sampleRate, bool applyHann)
{
    var detector = new McLeodPitchDetector(minFrequency: MinFreq, maxFrequency: MaxFreq);
    var freqs = new List<double>();
    var confs = new List<double>();
    int gatedOut = 0;

    for (int start = 0; start + WindowSize <= samples.Length; start += HopSize)
    {
        var window = new float[WindowSize];
        Array.Copy(samples, start, window, 0, WindowSize);

        SignalAnalyzer.RemoveDcOffset(window);
        if (applyHann) SignalAnalyzer.ApplyHannWindow(window);

        var r = detector.DetectPitch(window, sampleRate);
        if (!r.IsValid) continue;
        if (r.Confidence < MinConfidence) { gatedOut++; continue; }
        freqs.Add(r.Frequency);
        confs.Add(r.Confidence);
    }

    return new Stats(freqs, confs, gatedOut);
}

void Report(string label, Stats s, double? expectedHz)
{
    if (s.Freqs.Count == 0)
    {
        Console.WriteLine($"{label}: NO DETECTION (gated out {s.GatedOut})");
        return;
    }
    double median = Median(s.Freqs);
    double meanConf = s.Confs.Average();
    var (name, _) = NearestString(median);
    string centsStr = expectedHz is double e
        ? $"  cents-vs-expected: {Cents(median, e):+0.0;-0.0}"
        : "";
    Console.WriteLine($"{label}: median {median:F2} Hz -> nearest {name}  conf {meanConf:F2}  frames {s.Freqs.Count} (gated {s.GatedOut}){centsStr}");
}

(string Name, double Cents) NearestString(double hz)
{
    string best = "?"; double bestCents = double.MaxValue, signed = 0;
    foreach (var (name, f) in standard)
    {
        double c = Cents(hz, f);
        if (Math.Abs(c) < Math.Abs(bestCents)) { bestCents = c; best = name; signed = c; }
    }
    return (best, signed);
}

double? GuessExpectedFromName(string name)
{
    foreach (var (sName, hz) in standard)
        if (name.Contains(sName, StringComparison.OrdinalIgnoreCase)) return hz;
    return null;
}

static double Cents(double a, double b) => 1200.0 * Math.Log2(a / b);

static double Median(List<double> xs)
{
    var sorted = xs.OrderBy(x => x).ToList();
    int m = sorted.Count / 2;
    return sorted.Count % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2.0;
}

record Stats(List<double> Freqs, List<double> Confs, int GatedOut);
