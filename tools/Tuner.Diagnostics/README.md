# Tuner.Diagnostics

Offline harness that runs WAV files through the **real** detection DSP
(`McLeodPitchDetector` + the engine's per-window processing) so pitch accuracy
can be measured against known ground truth — no microphone or live capture
involved. This is how the "really off" bug was isolated and fixed.

## Usage

```sh
# Generate the synthetic ground-truth reference set (exact equal-tempered pitches)
dotnet run --project tools/Tuner.Diagnostics -- gen assets/reference

# Analyze one file (expected Hz optional; otherwise guessed from the filename)
dotnet run --project tools/Tuner.Diagnostics -- analyze assets/reference/synth_E2_82.41Hz_48k.wav 82.4069

# Analyze every WAV in a folder
dotnet run --project tools/Tuner.Diagnostics -- analyze-all assets/reference
```

For each file it reports the median detected frequency, nearest open string,
confidence, surviving frame count, and **cents vs expected** — computed both
*with* and *without* a Hann window so windowing artifacts are obvious.

## What it found

Every synthetic open string detects **dead-on (±0.0 cents, confidence 1.0)**
through the autocorrelation path — but applying a **Hann window** before the
NSDF biased the pitch **sharp**, worst at low frequencies:

| String | with Hann | no Hann |
|--------|-----------|---------|
| E2     | **+8.2¢** | 0.0¢    |
| A2     | +4.7¢     | 0.0¢    |
| D3     | +2.7¢     | 0.0¢    |
| G3     | +1.5¢     | 0.0¢    |
| B3     | +1.0¢     | 0.0¢    |
| E4     | +0.5¢     | 0.0¢    |

A Hann window is correct ahead of an **FFT**, but this detector is time-domain
autocorrelation/NSDF — tapering the ends shortens the effective overlap at long
lags and pulls the interpolated peak sharp. **Fix:** the engine no longer
windows the detection buffer (`TunerEngine.ProcessBuffer`). Locked in by
`PitchAccuracyTests` (asserts every open string within ±5 cents through the
full engine).

## Reference audio

- `synth_*.wav` — synthesized plucked-string tones (fundamental + 5 decaying
  partials) at exact pitches. A real recording is never guaranteed in tune;
  synthetic tones give cent-accurate ground truth. Regenerate with `gen`.
- `synthnoisy_*.wav` — same, plus −20 dB white noise, to stress octave-error
  robustness.
- `wikimedia_E2.{ogg,wav}` — real acoustic low-E recording, **"Guitar string
  E2" from Wikimedia Commons, licensed CC BY-SA 4.0**
  (https://commons.wikimedia.org/wiki/File:Guitar_string_E2.ogg). Reads 82.20 Hz
  (−4.4 cents) — i.e. a real guitar recorded slightly flat, correctly identified
  as E2. Converted to WAV with `ffmpeg -i in.ogg -ac 1 -c:a pcm_f32le out.wav`.

These files are gitignored (binary + licensed); regenerate the synthetic set
locally and re-download the real one as needed.
