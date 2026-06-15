# Guitar Tuner Testing Strategy

## Test Pyramid Overview

This project follows the test pyramid approach to ensure comprehensive coverage while maintaining fast feedback loops.

```
            /\
           /  \
          / UI \         ← Few (5-10)
         /      \           Slow, brittle, high confidence
        /────────\
       /          \
      / Integration \    ← Some (20-30)
     /              \       Medium speed, component boundaries
    /────────────────\
   /                  \
  /       Unit         \  ← Many (100+)
 /                      \    Fast, isolated, algorithmic
/────────────────────────\
```

> **Note:** Sections below the next divider describe the intended/aspirational
> test design. The two subsections immediately following document tests and tools
> that **actually exist in the repo today**.

## Offline DSP diagnostics & pitch-accuracy regression (implemented)

### `tools/Tuner.Diagnostics` — WAV → real-DSP harness

A console app that feeds WAV files through the **actual** detection pipeline
(`McLeodPitchDetector` + the engine's per-window processing) and reports the
median detected frequency, nearest open string, confidence, and **cents vs
expected** — computed both *with* and *without* a window so windowing artifacts
are obvious. This isolates DSP correctness from live microphone capture.

```bash
dotnet run --project tools/Tuner.Diagnostics -- gen assets/reference         # synthesize exact-pitch references
dotnet run --project tools/Tuner.Diagnostics -- analyze-all assets/reference # analyze a folder
dotnet run --project tools/Tuner.Diagnostics -- analyze file.wav 82.4069     # analyze one file (expected Hz)
```

Reference audio (`assets/reference/`) is gitignored; regenerate per the README.
It found the pitch-accuracy bug: a Hann window applied before the autocorrelation
detector biased low strings sharp (E2 +8.2¢). Without the window every synthetic
open string detects at ±0.0 cents.

### `PitchAccuracyTests` (Tuner.Integration.Tests)

Locks in accuracy (not just string identity) through the full `TunerEngine`:
each open string, played as a harmonic-rich tone, must be detected **within ±5
cents**. The pre-existing engine tests only asserted the string *name*, which is
how an 8-cent error shipped unnoticed. This test fails if a window is ever
re-introduced into the detection path.

---

## Unit Tests (Tuner.Core.Tests)

### Purpose
Test individual algorithms and components in complete isolation. These tests should:
- Run in milliseconds
- Have no external dependencies
- Be deterministic (same input → same output)
- Cover edge cases thoroughly

### Test Categories

#### 1. Pitch Detection Tests
```
PitchDetectorTests/
├── DetectsKnownFrequency_E2_82Hz
├── DetectsKnownFrequency_A4_440Hz
├── ReturnsLowConfidence_ForNoise
├── ReturnsLowConfidence_ForSilence
├── HandlesHarmonics_GuitarTimbre
├── IgnoresTransient_PluckAttack
└── AccuracyWithin1Cent_PureTone
```

**Test Data:** Pre-generated sine waves and recorded guitar samples stored as embedded resources.

#### 2. String Classification Tests
```
StringClassifierTests/
├── ClassifiesE2_WhenClosestToE2Target
├── ClassifiesA2_WhenClosestToA2Target
├── ReturnsNull_WhenOutOfRange
├── AppliesHysteresis_PreventsSwitching
├── HandlesCustomTuning_DropD
└── CalculatesCentsCorrectly
```

#### 3. Stability Filter Tests
```
StabilityFilterTests/
├── SmoothsFrequency_EMA
├── RequiresConsecutiveFrames_ForInTune
├── ResetsCounter_WhenOutOfTune
├── HandlesGaps_InInput
└── ConfigurableThresholds
```

#### 4. Cents Calculation Tests
```
CentsCalculatorTests/
├── Returns0_WhenExactMatch
├── ReturnsNegative_WhenFlat
├── ReturnsPositive_WhenSharp
├── Returns100_ForSemitone
├── Returns1200_ForOctave
└── HandlesBoundaryConditions
```

#### 5. Tuning Profile Tests
```
TuningProfileTests/
├── StandardTuning_HasCorrectFrequencies
├── DropD_HasCorrectFrequencies
├── CustomTuning_CanBeCreated
└── AllStrings_InValidRange
```

### Test Doubles

```csharp
// Mock audio input for testing engine without hardware
public class MockAudioInput : IAudioInput
{
    public void SimulateAudioFrame(float[] samples) { }
    public void SimulateDeviceDisconnect() { }
}

// Fake signal generator for testing pitch detection
public class SignalGenerator
{
    public static float[] GenerateSineWave(double frequency, int sampleRate, int samples);
    public static float[] GenerateGuitarTone(double frequency, int sampleRate, int samples);
    public static float[] GenerateWhiteNoise(int samples);
}
```

### Coverage Targets

| Component | Line Coverage | Branch Coverage |
|-----------|---------------|-----------------|
| PitchDetector | 95% | 90% |
| StringClassifier | 95% | 90% |
| StabilityFilter | 90% | 85% |
| TunerEngine | 85% | 80% |
| TuningProfiles | 100% | 100% |

## Integration Tests (Tuner.Integration.Tests)

### Purpose
Test component interactions and data flow through the system. These tests:
- May take seconds to run
- Test real component wiring
- Verify correct event propagation
- Test error handling paths

### Test Categories

#### 1. Engine Pipeline Tests
```
TunerEnginePipelineTests/
├── ProcessesAudioFrame_ProducesTunerFrame
├── StartStop_ManagedCorrectly
├── DeviceChange_ContinuesProcessing
├── LowSignal_ReportsTooQuiet
├── NoConfidence_ReportsUnstable
└── StableInput_ReportsInTune
```

#### 2. Audio Processing Tests
```
AudioProcessingTests/
├── HandlesVariousSampleRates_44100_48000
├── HandlesVariousBitDepths_16_32
├── HandlesMonoAndStereo
├── BuffersCorrectly_NoDropouts
└── ConvertsFormats_ToFloat32
```

#### 3. Event Flow Tests
```
EventFlowTests/
├── FrameEvents_FiredAtExpectedRate
├── StateChanges_PropagatedToUI
├── DeviceList_UpdatesOnChange
└── Errors_HandledGracefully
```

### Integration Test Fixtures

```csharp
public class TunerEngineFixture : IDisposable
{
    public ITunerEngine Engine { get; }
    public MockAudioInput AudioInput { get; }
    public List<TunerFrame> ReceivedFrames { get; }

    // Helper methods
    public void PlayNote(double frequency, int durationMs);
    public Task WaitForState(TunerState state, int timeoutMs);
    public void AssertInTuneWithin(double frequency, int toleranceCents, int timeoutMs);
}
```

### Test Data Files

```
TestData/
├── Audio/
│   ├── e2_82hz_guitar.wav
│   ├── a2_110hz_guitar.wav
│   ├── d3_147hz_guitar.wav
│   ├── g3_196hz_guitar.wav
│   ├── b3_247hz_guitar.wav
│   ├── e4_330hz_guitar.wav
│   ├── silence.wav
│   └── noise.wav
└── Profiles/
    └── custom_tunings.json
```

## UI Tests (Tuner.UI.Tests)

### Purpose
Verify critical user workflows work end-to-end. These tests:
- Are slow and potentially flaky
- Require actual UI rendering
- Test only the most critical paths
- Should be kept minimal

### Test Framework
- **xUnit** for test framework
- **FlaUI** for WPF automation

### Critical Path Tests (Keep Minimal)

```
UITests/
├── AppStarts_ShowsTunerView
├── DeviceDropdown_ListsAvailableDevices
├── TunerIndicator_MovesWithPitch
├── InTuneIndicator_ShowsWhenInTune
└── AppCloses_Gracefully
```

### UI Test Implementation

```csharp
public class TunerUITests : IClassFixture<ApplicationFixture>
{
    [Fact]
    public void AppStarts_ShowsTunerView()
    {
        // Arrange
        using var app = Application.Launch("Tuner.UI.Win.exe");
        var mainWindow = app.GetMainWindow();

        // Assert
        Assert.NotNull(mainWindow);
        Assert.True(mainWindow.FindFirstDescendant("TunerIndicator").IsVisible);
    }

    [Fact]
    public void DeviceDropdown_ListsAvailableDevices()
    {
        // Arrange
        using var app = Application.Launch("Tuner.UI.Win.exe");
        var dropdown = app.GetMainWindow().FindFirstDescendant("DeviceSelector");

        // Act
        dropdown.Click();

        // Assert
        var items = dropdown.FindAllDescendants().Where(x => x.ControlType == ControlType.ListItem);
        Assert.NotEmpty(items);
    }
}
```

### UI Test Guidelines

1. **Don't test styling** - Visual appearance is not testable via automation
2. **Don't test animations** - Too flaky, rely on manual verification
3. **Test data binding** - Verify data flows from ViewModel to View
4. **Test user actions** - Click, select, type
5. **Use explicit waits** - Never Thread.Sleep, always wait for conditions

## Test Execution Strategy

### Local Development

```bash
# Run all unit tests (should complete in < 10 seconds)
dotnet test tests/Tuner.Core.Tests

# Run integration tests (may take 30-60 seconds)
dotnet test tests/Tuner.Integration.Tests

# Run UI tests (requires display, takes 1-2 minutes)
dotnet test tests/Tuner.UI.Tests
```

### CI Pipeline

```yaml
stages:
  - name: Build
    steps:
      - dotnet build

  - name: Unit Tests
    steps:
      - dotnet test tests/Tuner.Core.Tests --logger trx
    failFast: true  # Stop pipeline if unit tests fail

  - name: Integration Tests
    steps:
      - dotnet test tests/Tuner.Integration.Tests --logger trx
    dependsOn: Unit Tests

  - name: UI Tests
    steps:
      - dotnet test tests/Tuner.UI.Tests --logger trx
    dependsOn: Integration Tests
    condition: scheduled  # Only run on nightly builds
```

### Test Naming Convention

```
[MethodUnderTest]_[Scenario]_[ExpectedBehavior]
```

Examples:
- `DetectPitch_PureSineWave440Hz_Returns440WithHighConfidence`
- `ClassifyString_FrequencyCloserToA2_ReturnsA2String`
- `FilterStability_ConsecutiveInTuneFrames_SetsStateToInTune`

## Mocking Strategy

### What to Mock

| Component | Mock In Unit Tests | Mock In Integration Tests |
|-----------|-------------------|---------------------------|
| IAudioInput | Always | Sometimes (for repeatable scenarios) |
| System clock | When testing timing | Sometimes |
| File system | If used | No |
| External APIs | Always | Always |

### What NOT to Mock

- **PitchDetector** - Test with real algorithm, use synthetic signals
- **StringClassifier** - Test with real cents calculation
- **StabilityFilter** - Test with real smoothing logic
- **TuningProfiles** - Use real data

## Test Data Management

### Synthetic Audio Generation

```csharp
public static class TestSignals
{
    // Generate pure sine wave at exact frequency
    public static float[] Sine(double frequency, int sampleRate, double duration)
    {
        int samples = (int)(sampleRate * duration);
        float[] buffer = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            buffer[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }
        return buffer;
    }

    // Generate guitar-like tone with harmonics
    public static float[] GuitarTone(double fundamental, int sampleRate, double duration)
    {
        // Add harmonics at 2f, 3f, 4f with decreasing amplitude
        // Add slight inharmonicity typical of guitar strings
    }

    // Add noise to signal
    public static float[] AddNoise(float[] signal, double snrDb) { }
}
```

### Real Audio Samples

Store recorded guitar samples for realistic testing:
- Record at 48kHz, 32-bit float
- Include pluck attack transient
- Include sustain portion
- Store as embedded resources or test data files

## Performance Testing

### Benchmarks (Run Manually)

```csharp
[MemoryDiagnoser]
public class PitchDetectorBenchmarks
{
    [Benchmark]
    public void DetectPitch_4096Samples()
    {
        var detector = new McLeodPitchDetector(48000);
        detector.DetectPitch(testBuffer);
    }
}
```

### Performance Targets

| Operation | Target | Maximum |
|-----------|--------|---------|
| Pitch detection (4096 samples) | < 2ms | 5ms |
| Full frame processing | < 5ms | 10ms |
| Memory per frame | < 1KB | 5KB |

## Mutation Testing (Optional)

Use Stryker.NET to verify test quality:

```bash
dotnet stryker -p Tuner.Core.csproj
```

Target: > 80% mutation score for critical algorithms

## Test Maintenance

### Flaky Test Policy

1. Flaky tests are bugs - fix or delete
2. Never ignore/skip flaky tests permanently
3. Add retry logic only as temporary measure
4. Track flaky test occurrences in CI

### Test Review Checklist

- [ ] Test name describes scenario and expectation
- [ ] Test has clear Arrange/Act/Assert sections
- [ ] Test is deterministic (no random, no current time)
- [ ] Test is isolated (no shared mutable state)
- [ ] Test runs fast (unit < 100ms, integration < 5s)
- [ ] Test failures provide clear diagnostic messages
