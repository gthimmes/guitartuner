using FluentAssertions;
using Tuner.AppContracts;
using Xunit;

namespace Tuner.Core.Tests;

public class StabilityFilterTests
{
    private const double MinConfidence = 0.8;
    private const double MinRms = 0.01;
    private const double Loud = 0.1;

    /// <summary>
    /// A filter with the pre-conditioning stages disabled so the smoothing /
    /// state logic can be tested in isolation.
    /// </summary>
    private static StabilityFilter PlainFilter(double alpha = 0.3, int stabilityFrames = 5) =>
        new(alpha, stabilityFrames, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1);

    private static FilteredResult Feed(StabilityFilter f, double freq, double cents, double conf = 0.9, double level = Loud) =>
        f.Process(freq, cents, conf, level, MinConfidence, MinRms);

    /// <summary>Feeds a frame of non-tonal room noise at the given level.</summary>
    private static FilteredResult Noise(StabilityFilter f, double level) =>
        f.Process(0, 0, 0.2, level, MinConfidence, MinRms);

    [Fact]
    public void Process_LowSignal_ReturnsTooQuiet()
    {
        var result = Feed(PlainFilter(), 110.0, 0, level: 0.001);

        result.State.Should().Be(TunerState.TooQuiet);
    }

    [Fact]
    public void Process_LowConfidence_WithNothingLocked_ReturnsListening()
    {
        var result = Feed(PlainFilter(), 110.0, 0, conf: 0.5);

        result.State.Should().Be(TunerState.Listening);
    }

    [Fact]
    public void Process_LowConfidence_WithNoteLocked_ReturnsUnstableAndKeepsReading()
    {
        var filter = PlainFilter();
        Feed(filter, 108.0, -10);

        var result = Feed(filter, 110.0, 0, conf: 0.5);

        result.State.Should().Be(TunerState.Unstable);
        result.Cents.Should().BeApproximately(-10, 0.01);
    }

    [Fact]
    public void Process_FlatPitch_ReturnsFlat()
    {
        var result = Feed(PlainFilter(), 108.0, -10);

        result.State.Should().Be(TunerState.Flat);
    }

    [Fact]
    public void Process_SharpPitch_ReturnsSharp()
    {
        var result = Feed(PlainFilter(), 112.0, 10);

        result.State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void Process_InTuneAfterStabilityFrames_ReturnsInTune()
    {
        var filter = PlainFilter(alpha: 0.5, stabilityFrames: 3);

        Feed(filter, 110.0, 1);
        Feed(filter, 110.0, 1);
        Feed(filter, 110.0, 1);
        var result = Feed(filter, 110.0, 1);

        result.State.Should().Be(TunerState.InTune);
    }

    [Fact]
    public void Process_BreakInTune_ResetsCounter()
    {
        var filter = PlainFilter(alpha: 0.5, stabilityFrames: 3);
        for (int i = 0; i < 5; i++)
            Feed(filter, 110.0, 1);

        Feed(filter, 110.0, 20);
        var result = Feed(filter, 110.0, 1);

        result.State.Should().NotBe(TunerState.InTune);
    }

    [Fact]
    public void Process_Smoothing_AppliesEMA()
    {
        var filter = PlainFilter(alpha: 0.3, stabilityFrames: 1);

        Feed(filter, 100.0, 0);
        var result = Feed(filter, 200.0, 0);

        // With alpha=0.3, smoothed = 0.3*200 + 0.7*100 = 130
        result.Frequency.Should().BeApproximately(130.0, 1.0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var filter = PlainFilter();
        Feed(filter, 110.0, 0);
        Feed(filter, 110.0, 0);

        filter.Reset();

        filter.SmoothedFrequency.Should().Be(0);
        filter.SmoothedCents.Should().Be(0);
    }

    // ---- Pre-conditioning stages -------------------------------------------

    [Fact]
    public void Median_RejectsSingleFrameOutlier()
    {
        // Median of 5, no smoothing, so the output is exactly the median.
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 5, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1);

        for (int i = 0; i < 5; i++)
            Feed(filter, 110.0, 2);
        // Octave error for one frame
        var result = Feed(filter, 220.0, 1200);

        result.Cents.Should().BeApproximately(2, 0.01);
        result.Frequency.Should().BeApproximately(110.0, 0.01);
        result.State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void SettleFrames_FirstReadingsAreHeldAsListening()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 3, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1);

        Feed(filter, 110.0, -8).State.Should().Be(TunerState.Listening);
        Feed(filter, 110.0, -8).State.Should().Be(TunerState.Listening);
        Feed(filter, 110.0, -8).State.Should().Be(TunerState.Flat);
    }

    [Fact]
    public void Onset_DiscardsAttackTransientReadings()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 4, jumpThresholdCents: 10_000, jumpConfirmFrames: 1, onsetRatio: 2.5);

        // Silence establishes a quiet envelope
        for (int i = 0; i < 5; i++)
            Feed(filter, 0, 0, conf: 0, level: 0.001);

        // Pluck: sharp, wobbly attack readings for the first frames
        Feed(filter, 113.0, 45, level: 0.3).State.Should().Be(TunerState.Listening);
        Feed(filter, 111.5, 25, level: 0.3).State.Should().Be(TunerState.Listening);
        Feed(filter, 108.0, -30, level: 0.3).State.Should().Be(TunerState.Listening);
        Feed(filter, 110.8, 12, level: 0.3).State.Should().Be(TunerState.Listening);

        // Settled
        var result = Feed(filter, 110.0, -3, level: 0.3);

        result.State.Should().Be(TunerState.Flat);
        result.Cents.Should().BeApproximately(-3, 0.01);
    }

    [Fact]
    public void Onset_RepluckKeepsLastReadingWhileHolding_ThenSnapsToNewPitch()
    {
        var filter = new StabilityFilter(smoothingAlpha: 0.3, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 3, jumpThresholdCents: 10_000, jumpConfirmFrames: 1, onsetRatio: 2.5);

        // Sustained note settled at -20 cents, decaying
        for (int i = 0; i < 20; i++)
            Feed(filter, 108.7, -20, level: 0.05);

        // Re-pluck after turning the peg: level jumps, attack readings garbage
        var held = Feed(filter, 130, 300, level: 0.4);
        held.State.Should().Be(TunerState.Flat, "the previous reading is kept during the hold");
        held.Cents.Should().BeApproximately(-20, 0.5);
        Feed(filter, 90, -300, level: 0.4);
        Feed(filter, 120, 100, level: 0.4);

        // First settled reading snaps rather than blending from -20
        var result = Feed(filter, 110.0, -1, level: 0.4);

        result.Cents.Should().BeApproximately(-1, 0.01);
    }

    [Fact]
    public void JumpGate_IgnoresBriefJump_ButAcceptsPersistentOne()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 30, jumpConfirmFrames: 3);

        for (int i = 0; i < 5; i++)
            Feed(filter, 110.0, -2);

        // Two-frame excursion: held at the previous value
        Feed(filter, 116.0, 90).Cents.Should().BeApproximately(-2, 0.01);
        Feed(filter, 116.0, 90).Cents.Should().BeApproximately(-2, 0.01);
        Feed(filter, 110.0, -2).Cents.Should().BeApproximately(-2, 0.01);

        // Persistent jump (different string): snaps after confirm frames
        Feed(filter, 116.0, 90).Cents.Should().BeApproximately(-2, 0.01);
        Feed(filter, 116.0, 90).Cents.Should().BeApproximately(-2, 0.01);
        var result = Feed(filter, 116.0, 90);

        result.Cents.Should().BeApproximately(90, 0.01);
        result.Frequency.Should().BeApproximately(116.0, 0.01);
    }

    // ---- Noise rejection ---------------------------------------------------

    [Fact]
    public void Agreement_RejectsReadingsThatDisagree_UntilTheyAgree()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 5, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            maxSpreadCents: 15);

        // Room noise that happens to pass the confidence test: a different
        // "pitch" every frame. Must never move the display.
        double[] junk = { -180, 95, 40, -60, 210, -30, 150, 5, -120, 70 };
        foreach (var c in junk)
        {
            var r = Feed(filter, 110 * Math.Pow(2, c / 1200.0), c);
            r.State.Should().Be(TunerState.Listening, $"junk reading {c} must not produce a pitch");
            r.Cents.Should().Be(0);
        }

        // A real note: consistent readings
        FilteredResult result = default;
        for (int i = 0; i < 5; i++)
            result = Feed(filter, 109.5, -8 + (i % 2));

        result.State.Should().Be(TunerState.Flat);
        result.Cents.Should().BeApproximately(-8, 1.5);
    }

    [Fact]
    public void Agreement_ToleratesOneOutlierInTheWindow()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 5, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            maxSpreadCents: 15);

        Feed(filter, 110, 3);
        Feed(filter, 110, 3);
        Feed(filter, 220, 1200); // one octave error
        Feed(filter, 110, 3);
        var result = Feed(filter, 110, 3);

        result.State.Should().Be(TunerState.Sharp);
        result.Cents.Should().BeApproximately(3, 0.01);
    }

    [Fact]
    public void NoiseGate_LearnsRoomNoise_AndIgnoresSoundNotClearlyAboveIt()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            noiseGateRatio: 3.0);

        // A noisy room: steady non-tonal sound well above the absolute threshold
        for (int i = 0; i < 300; i++)
            Noise(filter, 0.02);

        filter.NoiseFloor.Should().BeApproximately(0.02, 0.002);

        // Something only a little louder than the room (a voice, a door):
        // even if it reads as a pitch it's gated out.
        Feed(filter, 110, 0, level: 0.04).State.Should().Be(TunerState.TooQuiet);

        // A clearly louder played note gets through
        Feed(filter, 110, 0, level: 0.2).State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void NoiseGate_QuietRoom_StillUsesAbsoluteThreshold()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            noiseGateRatio: 3.0);

        for (int i = 0; i < 100; i++)
            Noise(filter, 0.0005);

        // 3x a near-silent floor is below MinRms, so MinRms is the gate
        Feed(filter, 110, 0, level: 0.005).State.Should().Be(TunerState.TooQuiet);
        Feed(filter, 110, 0, level: 0.02).State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void NoiseGate_SustainedNoteDoesNotRaiseTheFloor()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            noiseGateRatio: 3.0);

        for (int i = 0; i < 50; i++)
            Noise(filter, 0.003);

        // A long, loud note
        for (int i = 0; i < 500; i++)
            Feed(filter, 110, 0, level: 0.3);

        filter.NoiseFloor.Should().BeApproximately(0.003, 0.001);

        // ...and its quiet tail is still heard
        Feed(filter, 110, 0, level: 0.02).State.Should().NotBe(TunerState.TooQuiet);
    }

    [Fact]
    public void NoiseGate_FloorFallsQuicklyWhenRoomGoesQuiet()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            noiseGateRatio: 3.0);

        for (int i = 0; i < 300; i++)
            Noise(filter, 0.05);
        for (int i = 0; i < 20; i++)
            Noise(filter, 0.002);

        filter.NoiseFloor.Should().BeLessThan(0.004);
    }

    [Fact]
    public void NoiseGate_PluckAttackDoesNotRaiseTheFloor()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 1, onsetHoldFrames: 3, jumpThresholdCents: 10_000, jumpConfirmFrames: 1,
            onsetRatio: 2.5, noiseGateRatio: 3.0);

        for (int i = 0; i < 50; i++)
            Noise(filter, 0.003);

        // Loud, non-tonal attack frames, then the note
        Noise(filter, 0.5);
        Noise(filter, 0.4);
        for (int i = 0; i < 10; i++)
            Feed(filter, 110, 0, level: 0.3);

        filter.NoiseFloor.Should().BeApproximately(0.003, 0.001);
    }
}
