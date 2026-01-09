Here’s a solid “clean separation” tech stack + a set of requirements that will let you build a **real-time guitar tuner** now on Windows, while keeping the back end reusable for other UIs/platforms later.

## Recommended tech stack

### Core (reusable back end)

**.NET 8 (or .NET 9) + a pure .NET “tuner engine” library**

- **Project type:** Class Library

- **Responsibilities:** audio device abstraction, audio capture pipeline, pitch detection, note/string classification, stability filtering, “sharp/flat” math, events/streams for UI

**Audio capture (Windows now)**

- **NAudio** (best-known .NET audio I/O for Windows)
  
  - Use **WASAPI capture** for low latency.
  
  - Enumerate devices, choose default, switch devices at runtime.

- Alternative: **CSCore** (also good, some prefer it). Pick one; NAudio is the usual default.

**DSP / pitch detection**

- Option A (fastest to implement): **NWaves** (DSP primitives + FFT, filters, windowing). You still implement pitch detection algorithm on top.

- Option B (you implement yourself): a focused pitch detector (recommended for tuner UX):
  
  - **YIN** (good accuracy for monophonic signals like a single string)
  
  - **MPM (McLeod Pitch Method)** (also great)
  
  - (FFT peak picking works, but tends to jitter more on guitar fundamentals unless you do extra harmonic handling.)

**Packaging**

- `Tuner.Core` (pure logic: tuning profiles, note math, smoothing)

- `Tuner.Audio` (interfaces + implementations; Windows implementation uses NAudio)

- `Tuner.AppContracts` (DTOs/events/interfaces used by UIs)

- Optional: `Tuner.Service` (hosted service wrapper if you want a process boundary)

### UI layer(s) (swappable)

Pick whatever UI per platform; keep them “dumb,” consuming updates from the engine.

- **Windows desktop UI**: WinUI 3 or WPF (WPF is fastest to ship; WinUI 3 is modern)

- **Cross-platform later**: .NET MAUI (if you want one UI codebase) or separate native UIs per platform

### Clean separation pattern

Use one of these (both work):

1. **In-process, layered library approach (simplest)**
- UI references `Tuner.Core` + `Tuner.Audio`

- UI binds to events or an `IObservable<TunerFrame>`
2. **Process boundary (strongest separation)**
- Back end runs as a local service (gRPC or named pipes)

- UIs talk to it via a contract

- This is great if you’ll have multiple UIs or want to share one running engine

If you’re building only one Windows UI now, do **(1)** and keep the design compatible with moving to **(2)** later.

---

## Functional requirements

### Audio input

- Use **default Windows input device** on startup

- Allow selecting a different input device (microphone, interface, etc.)

- Show:
  
  - device name
  
  - sample rate / channel count
  
  - live input meter (optional but helpful)

- Support common formats:
  
  - 44.1kHz and 48kHz
  
  - 16-bit / 32-bit float (normalize internally to float)

### Tuning behavior

- Default tuning profile: **Standard guitar**:  
  E2 (82.41 Hz), A2 (110), D3 (146.83), G3 (196), B3 (246.94), E4 (329.63)

- Architecture supports additional tunings later:
  
  - Drop D, half-step down, open tunings, custom user-defined

- **Single-string mode** (your described workflow):
  
  - User plays one string at a time
  
  - System auto-detects which target string they’re closest to
  
  - Shows **flat/sharp** clearly and **in-tune** when within tolerance

- Optional future mode:
  
  - “Target string lock” (user taps E/A/D/G/B/E to lock detection)

### Real-time output model (what the UI receives)

Emit a “frame” at ~20–60 Hz containing:

- detected frequency (Hz)

- confidence (0–1)

- detected note name (optional)

- cents offset from nearest target string

- target string (E2/A2/…)

- state: `Unknown | Listening | TooQuiet | Unstable | Flat | Sharp | InTune`

### Pitch detection constraints

- Designed for **monophonic** input (one string at a time)

- Needs to handle:
  
  - noisy input
  
  - harmonics (guitar fundamental can be quieter than harmonics)
  
  - pluck transients (ignore first ~30–80ms after attack)

---

## Non-functional requirements

### Latency and responsiveness

- End-to-end latency target: **< 50ms** feels good

- Processing window:
  
  - Use ~2048–4096 samples at 48kHz (≈43–85ms) *with overlap*
  
  - Hop size: 256–512 samples for smoother updates

### Stability / jitter control

To avoid a “twitchy” tuner:

- Smoothing filter on frequency or cents (EMA / median of last N frames)

- “Stable pitch” gating:
  
  - require N consecutive frames within tolerance before showing “In Tune”

- Drop frames when confidence is low

### Accuracy

- For guitar tuner UX: **±1–2 cents** display resolution; **±3–5 cents** “in tune” threshold

- Allow configurable tolerance per tuning profile or globally

### Robustness

- Auto-handle device disconnect/reconnect

- Fail gracefully when no input device exists

- CPU budget: should run easily under a few % on typical machines

---

## Core algorithms / logic requirements

### String selection (auto-detect which string)

When you have a detected frequency `f`:

- Compute cents distance to each string’s target frequency in active tuning:
  
  - `cents = 1200 * log2(f / f_target)`

- Choose the string with smallest `abs(cents)` **if within a max range**
  
  - e.g. must be within ±250 cents (~2.5 semitones) to avoid misclassification

- Add hysteresis:
  
  - don’t switch strings unless a new string is clearly closer for a short duration

### “Flat / Sharp / In-tune”

- `InTune` if `abs(cents) <= toleranceCents` (e.g. 5)

- else `Flat` if cents < 0, `Sharp` if cents > 0

### Confidence

Confidence can be computed via:

- YIN/MPM internal confidence measure

- plus RMS threshold (too quiet)

- plus stability over last N frames

---

## API / contracts (clean UI boundary)

Define interfaces like:

- `IAudioInput`
  
  - `IReadOnlyList<AudioDevice> ListDevices()`
  
  - `Task SetDevice(deviceId)`
  
  - `event AudioFrameReceived(float[] samples, int sampleRate, int channels)`

- `ITunerEngine`
  
  - `void SetTuning(TuningProfile profile)`
  
  - `IObservable<TunerFrame> Frames` (or `event EventHandler<TunerFrame>`)
  
  - `void Start() / Stop()`

This keeps UI ignorant of audio APIs + DSP details.

---

## Suggested project layout

- `Tuner.AppContracts`  
  DTOs: `TunerFrame`, `TuningProfile`, `StringTarget`, enums

- `Tuner.Core`  
  Pitch detection + string classification + smoothing + tunings

- `Tuner.Audio.Abstractions`  
  `IAudioInput`, `AudioDevice`

- `Tuner.Audio.Windows`  
  NAudio WASAPI implementation

- `Tuner.UI.Win` (WPF/WinUI3)  
  Pure presentation

(If you later want a service boundary: add `Tuner.EngineHost` + `Tuner.Client` via gRPC.)

---

## Acceptance criteria (what “done” looks like)

1. On launch, tuner uses default input device and starts listening within 1 second

2. User can select a different input device and tuning continues without restart

3. Playing open strings one at a time correctly identifies E/A/D/G/B/E within 0.5s

4. UI shows flat/sharp and reaches “In Tune” when within ±5 cents for ≥300ms

5. Doesn’t jump between strings while sustaining a note (hysteresis works)

6. Works on common USB audio interfaces as well as laptop mic
