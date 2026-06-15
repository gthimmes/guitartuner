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
- `AudioFrameEventArgs` - Audio sample data container

**Dependencies:** Tuner.AppContracts

### Tuner.Audio.Windows

**Purpose:** Windows-specific audio capture using NAudio.

**Implementation:**
- WASAPI capture for low latency (~10-20ms)
- Device enumeration and hot-swap support
- Automatic format conversion to float samples
- Handles device disconnect/reconnect gracefully

**Dependencies:**
- Tuner.Audio.Abstractions
- NAudio (NuGet package)

### Tuner.UI.Win

**Purpose:** WPF presentation layer. "Dumb" UI that only displays data.

**Architecture:** MVVM pattern
- `MainViewModel` - Binds to TunerEngine events
- `TunerView` - Visual tuner display (needle/indicator)
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

```csharp
// Exponential Moving Average
smoothedFreq = alpha * newFreq + (1 - alpha) * smoothedFreq

// InTune requires N consecutive frames
if (|cents| <= tolerance for N frames) state = InTune
```

## Configuration

Key parameters are configurable:

| Parameter | Default | Description |
|-----------|---------|-------------|
| InTuneTolerance | 5 cents | Threshold for "in tune" state |
| StabilityFrames | 15 | Consecutive frames for InTune |
| SmoothingAlpha | 0.3 | EMA smoothing factor |
| MinConfidence | 0.8 | Minimum pitch confidence |
| MinRmsThreshold | 0.01 | Minimum signal level |
| HysteresisFrames | 10 | Frames before string switch |

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
