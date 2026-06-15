# Guitar Tuner

A Windows desktop guitar tuner application built with C# and WPF.

## Features

- Real-time pitch detection using McLeod Pitch Method (MPM)
- Support for multiple tuning profiles (Standard, Drop D, Half Step Down, Open G)
- Visual tuning indicator with sharp/flat feedback
- Low-latency audio capture via WASAPI

## Requirements

- Windows 10/11
- .NET 8.0 SDK or newer (projects target `net8.0`; SDK 9/10 build fine via roll-forward)
- A microphone or audio input device
- [ffmpeg](https://ffmpeg.org/) on PATH — only needed to (re)download the real
  reference recording for the diagnostics harness (see below)

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run --project src/Tuner.UI.Win
```

## Testing

```bash
dotnet test
```

## Offline pitch-detection diagnostics

`tools/Tuner.Diagnostics` runs WAV files through the **real** detection DSP and
reports accuracy in cents against known ground truth — no microphone needed.
This is the harness used to find and fix the pitch-accuracy bug (a stray Hann
window that biased low strings ~+8 cents sharp; see
[tools/Tuner.Diagnostics/README.md](tools/Tuner.Diagnostics/README.md)).

The reference audio under `assets/reference/` is **gitignored** (binary +
CC-licensed), so regenerate it on a fresh checkout:

```bash
# 1. Synthetic ground-truth set for all six open strings (exact pitches)
dotnet run --project tools/Tuner.Diagnostics -- gen assets/reference

# 2. (optional) Real low-E recording — Wikimedia Commons, CC BY-SA 4.0
curl -L "https://upload.wikimedia.org/wikipedia/commons/0/0a/Guitar_string_E2.ogg" -o assets/reference/wikimedia_E2.ogg
ffmpeg -y -i assets/reference/wikimedia_E2.ogg -ac 1 -c:a pcm_f32le assets/reference/wikimedia_E2.wav

# 3. Analyze everything (cents vs expected, with/without window for comparison)
dotnet run --project tools/Tuner.Diagnostics -- analyze-all assets/reference
```

### Verifying on a machine with a microphone

The harness proves the DSP is correct; to validate the **live capture path**,
record an open string to a WAV (48 kHz mono) and analyze it:

```bash
dotnet run --project tools/Tuner.Diagnostics -- analyze your_recording.wav 82.4069
```

If a recorded WAV reads correctly but the live tuner UI does not, the bug is in
audio capture, not detection — the prime suspect is the sample rate the engine
assumes (`CurrentDevice.SampleRate`) not matching what WASAPI actually delivers
(`TunerEngine.ProcessingLoop` / `WasapiAudioInput.OnDataAvailable`).

## Architecture

The solution follows clean architecture principles:

- **Tuner.Core** - Platform-independent pitch detection and signal processing
- **Tuner.AppContracts** - Shared interfaces and data types
- **Tuner.Audio.Abstractions** - Audio input interface
- **Tuner.Audio.Windows** - Windows WASAPI audio implementation
- **Tuner.UI.Win** - WPF user interface

See [ARCHITECTURE.md](ARCHITECTURE.md) for details.

## License

MIT
