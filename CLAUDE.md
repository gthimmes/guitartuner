# Guitar Tuner — agent context

Windows WPF guitar tuner (C#/.NET). Real-time pitch detection via the McLeod
Pitch Method (MPM). Layered, testable architecture; see `ARCHITECTURE.md`.

## Layout

- `src/Tuner.Core` — DSP: `McLeodPitchDetector`, `TunerEngine`, `SignalAnalyzer`,
  `StringClassifier`, `StabilityFilter`, `CentsCalculator`. Platform-independent.
- `src/Tuner.AppContracts` — shared types (`TunerConfiguration`, `TunerFrame`, …).
- `src/Tuner.Audio.Abstractions` / `src/Tuner.Audio.Windows` — `IAudioInput` /
  `IReferenceTonePlayer` and the NAudio implementations (`WasapiAudioInput`,
  `WaveOutReferenceTonePlayer`).
- `src/Tuner.UI.Win` — WPF UI.
- `tests/` — `Tuner.Core.Tests`, `Tuner.Integration.Tests`, `Tuner.UI.Tests`.
- `tools/Tuner.Diagnostics` — offline WAV→DSP harness (see its README).

Build: `dotnet build`. Test: `dotnet test` (all should pass; 145 at last count).

## Stability pipeline (jitter and noise before lock-in)

Two live symptoms: the reading "jumping above and below" for a moment after
each pluck, and "any noise in the room makes the scale jump all over the place"
(other tuners don't react to every sound). `StabilityFilter` runs, in order:
adaptive noise gate (learns the RMS of non-tonal room sound; a signal must be
`NoiseGateRatio`× louder — notes and pluck attacks never raise the floor),
onset hold (RMS jump over a slow envelope → discard the attack transient for
`OnsetHoldFrames`), confidence gate (`MinConfidence` 0.8; real notes read
0.95+), running median (`MedianWindow`, full window required), agreement check
(all but one reading within `MaxSpreadCents` of the median — noise disagrees
with itself frame to frame), jump gate (a step > `JumpThresholdCents` must
persist `JumpConfirmFrames` then snaps), then the EMA. With nothing locked,
holds report `Listening` (UI idle) not `Unstable`. Diagram + knob table in
`ARCHITECTURE.md`. Tune the knobs in `TunerConfiguration`, not by adding more
smoothing in the UI.

## Reference tone mode

Header has a Microphone / Reference Tone switch (`MainViewModel.IsToneMode`).
In tone mode the string buttons play that string's pitch through the speakers
(`ReferenceToneSynthesizer` in Core: fundamental + 5 harmonics, 20 ms ramps;
`WaveOutReferenceTonePlayer` in Audio.Windows) and `TunerFrame`s are ignored
because the mic would hear the speaker. Works without a microphone. Synth pitch
is guarded by `ReferenceToneSynthesizerTests` (detector reads it within 1¢).

## UI indicator

`Controls/TunerIndicator` is a `FrameworkElement` drawn in `OnRender` (no XAML
template): vertical scale, needle moves UP when sharp / DOWN when flat, inner
±10¢ occupies half the scale for fine resolution, big arrow above = tune up
(flat), below = tune down (sharp). Needle glides via a 70 ms animation on a
private `DisplayCents` DP; off-screen it snaps so it can be rendered in tests.

## Known-fixed bug: pitch read sharp ("really off")

**Symptom:** detection was consistently off, worst on low E.
**Root cause:** `TunerEngine.ProcessBuffer` applied a **Hann window** before the
autocorrelation/NSDF detector. Windowing is only valid ahead of an FFT; for this
time-domain method it biased the pitch **sharp** (+8.2¢ on E2, scaling down with
frequency). **Fix:** removed the window call — detection buffer now gets
DC-offset removal only.
**Guarded by:** `tests/Tuner.Integration.Tests/PitchAccuracyTests.cs` (every open
string within ±5 cents through the full engine). Do not re-add any window to the
detection path.

## Diagnostics workflow (no mic needed)

`tools/Tuner.Diagnostics` runs WAVs through the real DSP and prints cents-vs-
expected, computed **with and without** a window for comparison. Reference audio
in `assets/reference/` is **gitignored** (binary + CC-licensed) — regenerate:

```bash
dotnet run --project tools/Tuner.Diagnostics -- gen assets/reference        # synthetic exact-pitch set
dotnet run --project tools/Tuner.Diagnostics -- analyze-all assets/reference # report accuracy
dotnet run --project tools/Tuner.Diagnostics -- analyze file.wav 82.4069     # one file, expected Hz
```

Real low-E sample (Wikimedia Commons, CC BY-SA 4.0) — needs `ffmpeg`:
```bash
curl -L "https://upload.wikimedia.org/wikipedia/commons/0/0a/Guitar_string_E2.ogg" -o assets/reference/wikimedia_E2.ogg
ffmpeg -y -i assets/reference/wikimedia_E2.ogg -ac 1 -c:a pcm_f32le assets/reference/wikimedia_E2.wav
```

## Live microphone path: verified

Tested on real hardware with a guitar (Aug 2026): pitch reads correctly live, so
the capture path (sample rate from `_audioInput.CurrentDevice?.SampleRate`) is
fine on the tested device. If a *different* device ever reads off by a constant
ratio, that sample-rate assumption in `TunerEngine.ProcessingLoop` is still the
first place to look (each `AudioFrameEventArgs` also carries its own rate).

Standard tuning targets (Hz): E2 82.41, A2 110.00, D3 146.83, G3 196.00,
B3 246.94, E4 329.63.
