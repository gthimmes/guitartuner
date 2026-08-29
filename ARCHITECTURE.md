# Guitar Tuner Architecture

## Overview

This document describes the architecture of the Guitar Tuner application, designed with clean separation of concerns to enable multiple UI frontends while sharing a common core logic.

## Design Principles

### 1. Separation of Concerns
- **Core logic** is completely decoupled from UI and platform-specific code
- **Audio capture** is abstracted behind interfaces for platform portability
- **UI layer** is a thin presentation layer that only consumes data from the engine

### 2. Dependency Direction
```
UI Layer → App Contracts ← Core Logic
              ↑
        Audio Abstractions ← Audio Implementation (Windows)
```

All dependencies point inward toward abstractions. Concrete implementations depend on abstractions, not vice versa.

### 3. Testability
- Core algorithms are pure functions where possible
- All external dependencies are injected via interfaces
- State changes are observable and predictable

## Project Structure

```
GuitarTuner/
├── src/
│   ├── Tuner.AppContracts/      # Shared DTOs, enums, and interfaces
│   ├── Tuner.Core/              # Pure business logic (no platform deps)
│   ├── Tuner.Audio.Abstractions/ # Audio input interfaces
│   ├── Tuner.Audio.Windows/     # NAudio/WASAPI implementation
│   └── Tuner.UI.Win/            # WPF presentation layer
├── tests/
│   ├── Tuner.Core.Tests/        # Unit tests for core logic
│   ├── Tuner.Integration.Tests/ # Integration tests
│   └── Tuner.UI.Tests/          # UI automation tests
└── docs/
    ├── ARCHITECTURE.md          # This file
    └── TESTING.md               # Testing strategy
```

## Layer Descriptions

### Tuner.AppContracts

**Purpose:** Define the contract between all layers. Contains no logic.

**Contents:**
- `TunerFrame` - Real-time output data structure
- `TuningProfile` - Definition of a tuning (standard, drop D, etc.)
- `StringTarget` - Individual string frequency target
- `TunerState` enum - Unknown, Listening, TooQuiet, Unstable, Flat, Sharp, InTune
- `AudioDevice` - Audio device descriptor
- `ITunerEngine` - Main engine interface
- `IAudioInput` - Audio capture interface

**Dependencies:** None (this is the innermost layer)

### Tuner.Core

**Purpose:** All pitch detection, signal processing, and tuning logic.

**Key Components:**

1. **PitchDetector** - Implements McLeod Pitch Method (MPM) algorithm
   - Autocorrelation-based pitch detection
   - Parabolic interpolation for sub-sample accuracy
   - Confidence estimation

2. **StringClassifier** - Determines which string is being played
   - Cents calculation from detected frequency
   - Hysteresis to prevent string jumping
   - Configurable tolerance thresholds

3. **StabilityFilter** - Smooths output to prevent jitter
   - Exponential moving average (EMA) for frequency
   - Consecutive frame requirement for "InTune" state
   - Configurable window sizes

4. **TuningProfiles** - Predefined and custom tuning definitions
   - Standard: E2, A2, D3, G3, B3, E4
   - Drop D, Half-step down, etc.
   - Support for custom tunings

5. **TunerEngine** - Orchestrates all components
   - Consumes audio frames from IAudioInput
   - Produces TunerFrame events at 20-60 Hz
   - Manages lifecycle (Start/Stop)

**Dependencies:** Tuner.AppContracts

### Tuner.Audio.Abstractions

**Purpose:** Platform-agnostic audio interfaces.

**Contents:**
- `IAudioInput` interface
- `IReferenceTonePlayer` interface (play a pitch through the speakers)
- `AudioFrameEventArgs` - Audio sample data container

**Dependencies:** Tuner.AppContracts

### Tuner.Audio.Windows

**Purpose:** Windows-specific audio capture using NAudio.

**Implementation:**
- `WasapiAudioInput`: WASAPI capture for low latency (~10-20ms), device
  enumeration and hot-swap, format conversion to float samples, graceful
  disconnect/reconnect
- `WaveOutReferenceTonePlayer`: plays `Tuner.Core.ReferenceToneSynthesizer`
  through the default output via NAudio `WaveOutEvent`

**Dependencies:**
- Tuner.Audio.Abstractions
- Tuner.Core (for the tone synthesizer)
- NAudio (NuGet package)

### Tuner.UI.Win

**Purpose:** WPF presentation layer. "Dumb" UI that only displays data.

**Architecture:** MVVM pattern
- `MainViewModel` - Binds to TunerEngine events; owns the Microphone /
  Reference Tone mode switch (`IsToneMode`)
- `Controls/TunerIndicator` - Visual tuner display (vertical gauge, arrows)
- `DeviceSelector` - Audio device dropdown

**Key Principles:**
- No business logic in UI
- All state comes from TunerFrame
- UI only sends commands (device selection, start/stop)

**Dependencies:**
- Tuner.AppContracts
- Tuner.Core
- Tuner.Audio.Windows
- Microsoft.Extensions.DependencyInjection

## Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        Audio Pipeline                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐  │
│  │ WASAPI   │───→│ NAudio   │───→│ Format   │───→│ IAudio   │  │
│  │ Capture  │    │ Buffer   │    │ Convert  │    │ Input    │  │
│  └──────────┘    └──────────┘    └──────────┘    └──────────┘  │
│                                                       │         │
└───────────────────────────────────────────────────────┼─────────┘
                                                        │
                                                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Processing Pipeline                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐  │
│  │ Audio    │───→│ Pitch    │───→│ String   │───→│ Stability│  │
│  │ Frame    │    │ Detector │    │ Classify │    │ Filter   │  │
│  └──────────┘    └──────────┘    └──────────┘    └──────────┘  │
│                                                       │         │
└───────────────────────────────────────────────────────┼─────────┘
                                                        │
                                                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Output Pipeline                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                   │
│  │ Tuner    │───→│ IObserv- │───→│ UI       │                   │
│  │ Frame    │    │ able<T>  │    │ Binding  │                   │
│  └──────────┘    └──────────┘    └──────────┘                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Algorithms

### McLeod Pitch Method (MPM)

Selected for:
- High accuracy on monophonic signals
- Good handling of guitar harmonics
- Built-in confidence measure
- Efficient O(n log n) with FFT-based autocorrelation

```
1. Compute normalized square difference function (NSDF)
2. Find peaks in NSDF
3. Select highest peak above threshold
4. Apply parabolic interpolation
5. Convert lag to frequency: f = sampleRate / lag
```

> **Do NOT apply a Hann (or any) window before detection.** MPM is a
> time-domain autocorrelation/NSDF method. A window is only correct ahead of an
> FFT; tapering the buffer ends here shortens the effective overlap at long lags
> and biases the interpolated peak **sharp** — measured at **+8 cents on low E**.
> The detection buffer gets DC-offset removal only (`TunerEngine.ProcessBuffer`).
> This is verified by `PitchAccuracyTests` and the `Tuner.Diagnostics` harness.

### Cents Calculation

```csharp
cents = 1200 * Math.Log2(detectedFreq / targetFreq)
```

- Negative cents = flat
- Positive cents = sharp
- |cents| <= tolerance = in tune

### Stability Filtering

Raw readings from a microphone are noisy in several distinct ways; the
`StabilityFilter` has one stage for each, in this order:

```
raw (Hz, cents, confidence, RMS)
  │
  ├─ noise gate: level < max(MinRms, noiseFloor × NoiseGateRatio) ▶ TooQuiet
  │     noiseFloor = RMS of non-tonal sound (fans, voices, clatter), learned
  │     from frames that fail the confidence check: rises slowly (~0.5 s),
  │     falls fast. A note, or a pluck's attack burst, never raises it, so a
  │     long sustained note is not mistaken for a noisier room.
  │
  ├─ onset?  RMS > envelope × OnsetRatio           new pluck: clear history,
  │          (envelope = slow EMA of RMS)           hold OnsetHoldFrames frames
  │                                                 (the attack transient reads
  │                                                  sharp and inharmonic)
  ├─ confidence < MinConfidence ──────────────────▶ note locked: Unstable
  │                                                 (keeps last value);
  │                                                 nothing locked: Listening
  │                                                 (UI stays idle)
  ├─ running median over MedianWindow readings      drops single-frame outliers
  │  (needs a full window before reporting)         (octave errors, harmonics)
  │
  ├─ agreement: all but one reading within         noise that sneaks past the
  │  ±MaxSpreadCents of the median, else hold       confidence check reads a
  │                                                  different pitch every frame
  ├─ jump gate: |median − smoothed| > JumpThreshold  ignore until it persists
  │             for JumpConfirmFrames, then SNAP     JumpConfirmFrames (new
  │                                                  string / big peg turn)
  ├─ EMA:  smoothed = α·median + (1−α)·smoothed
  │
  └─ |cents| ≤ tolerance for StabilityFrames ──────▶ InTune, else Flat / Sharp
```

While holding (attack, unconfirmed jump, not yet settled) the filter re-reports
the last good reading so the needle sits still instead of twitching. When
nothing is locked yet, holds report `Listening` rather than `Unstable`, so
room noise leaves the display idle instead of showing a twitching needle and
"Hold steady".

### Reference Tone

An alternative to the microphone: play the target pitch and tune by ear.

- `Tuner.Core.ReferenceToneSynthesizer` - additive synthesis, fundamental +
  five harmonics with plucked-string-like amplitudes (a bare 82 Hz sine is
  inaudible on laptop speakers), 20 ms fade in/out so there are no clicks,
  phase-continuous pitch changes. Pure DSP, unit-tested by running its output
  through `McLeodPitchDetector` (within 1 cent on all six strings).
- `WaveOutReferenceTonePlayer` (Audio.Windows) wraps it as an NAudio
  `ISampleProvider`; the output device is released ~250 ms after Stop.
- `MainViewModel.IsToneMode` switches the UI: the gauge is replaced by the
  playing string + "Tune until the beating stops", the string buttons play /
  stop tones, and incoming `TunerFrame`s are ignored (the microphone would
  hear the speaker).

## Configuration

Key parameters are configurable (`TunerConfiguration`):

| Parameter | Default | Description |
|-----------|---------|-------------|
| InTuneTolerance | 5 cents | Threshold for "in tune" state |
| StabilityFrames | 8 | Consecutive in-tolerance frames for InTune |
| SmoothingAlpha | 0.3 | EMA smoothing factor |
| MedianWindow | 5 | Readings in the outlier-rejecting median |
| OnsetHoldFrames | 6 | Frames ignored after a pluck is detected |
| OnsetRatio | 2.5 | RMS / envelope ratio that counts as a pluck |
| JumpThresholdCents | 30 | Step size treated as a jump, not a drift |
| JumpConfirmFrames | 4 | Frames a jump must persist before it is shown |
| NoiseGateRatio | 3 | Sound must be this much louder than the learned noise floor |
| MaxSpreadCents | 15 | Median-window readings must agree this closely |
| MinConfidence | 0.8 | Minimum pitch confidence (NSDF clarity) |
| MinRmsThreshold | 0.002 | Absolute minimum signal level |
| HysteresisFrames | 5 | Frames before string switch |
| WindowSize / HopSize | 4096 / 512 | Analysis window and hop (samples) |

## Extensibility Points

### Adding New Tunings
1. Create new `TuningProfile` with target frequencies
2. Register in `TuningProfiles` static collection
3. No code changes needed elsewhere

### Adding New Platform
1. Implement `IAudioInput` for target platform
2. Create new UI project referencing Core
3. Wire up with dependency injection

### Adding Service Boundary
1. Create `Tuner.EngineHost` with gRPC server
2. Create `Tuner.Client` with gRPC client implementing ITunerEngine
3. UI references Client instead of Core directly

## Performance Considerations

### Audio Buffer Sizing
- Window: 4096 samples (~85ms at 48kHz) for frequency resolution
- Hop: 512 samples (~10ms) for responsive updates
- Results in ~100 Hz internal processing rate

### Thread Model
- Audio capture: Dedicated callback thread (NAudio managed)
- Processing: ThreadPool via async/await
- UI updates: Marshaled to UI thread via dispatcher

### Memory
- Circular buffer for audio samples
- Object pooling for TunerFrame
- No allocations in hot path

## Error Handling

### Device Errors
- Auto-retry on device disconnect
- Graceful fallback to default device
- User notification via TunerState.Unknown

### Processing Errors
- Low confidence → TunerState.Unstable
- Low signal → TunerState.TooQuiet
- No exceptions thrown to UI layer

## Future Considerations

1. **Polyphonic detection** - Detect multiple strings simultaneously
2. **Recording** - Save tuning sessions
3. **Visual metronome** - Combine with tempo training
4. **Alternate instruments** - Bass, ukulele, violin
5. **Cloud sync** - Custom tuning profiles across devices
