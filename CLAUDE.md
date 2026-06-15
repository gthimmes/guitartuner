# Guitar Tuner — agent context

Windows WPF guitar tuner (C#/.NET). Real-time pitch detection via the McLeod
Pitch Method (MPM). Layered, testable architecture; see `ARCHITECTURE.md`.

## Layout

- `src/Tuner.Core` — DSP: `McLeodPitchDetector`, `TunerEngine`, `SignalAnalyzer`,
  `StringClassifier`, `StabilityFilter`, `CentsCalculator`. Platform-independent.
- `src/Tuner.AppContracts` — shared types (`TunerConfiguration`, `TunerFrame`, …).
- `src/Tuner.Audio.Abstractions` / `src/Tuner.Audio.Windows` — `IAudioInput` and
  the NAudio/WASAPI implementation (`WasapiAudioInput`).
- `src/Tuner.UI.Win` — WPF UI.
- `tests/` — `Tuner.Core.Tests`, `Tuner.Integration.Tests`, `Tuner.UI.Tests`.
- `tools/Tuner.Diagnostics` — offline WAV→DSP harness (see its README).

Build: `dotnet build`. Test: `dotnet test` (all should pass; 115 at last count).

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

## Open / next step: live microphone path

The harness proves the **DSP** is correct. The **live capture path** has not been
verified on hardware. If the tuner is still off live while recorded WAVs analyze
correctly, the bug is in capture, not detection. **Prime suspect:**
`TunerEngine.ProcessingLoop` reads the sample rate from
`_audioInput.CurrentDevice?.SampleRate` (set once from the WASAPI *mix* format),
while each frame also carries its own rate in `AudioFrameEventArgs`. If those
ever disagree, `f = sampleRate / lag` is scaled wrong and everything reads off by
that ratio. To validate: record an open string to 48 kHz mono WAV and run it
through the harness; compare to the live UI reading.

Standard tuning targets (Hz): E2 82.41, A2 110.00, D3 146.83, G3 196.00,
B3 246.94, E4 329.63.
