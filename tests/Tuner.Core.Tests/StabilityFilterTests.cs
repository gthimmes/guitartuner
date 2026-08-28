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

    [Fact]
    public void Process_LowSignal_ReturnsTooQuiet()
    {
        var result = Feed(PlainFilter(), 110.0, 0, level: 0.001);

        result.State.Should().Be(TunerState.TooQuiet);
    }

    [Fact]
    public void Process_LowConfidence_ReturnsUnstable()
    {
        var result = Feed(PlainFilter(), 110.0, 0, conf: 0.5);

        result.State.Should().Be(TunerState.Unstable);
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

        Feed(filter, 110.0, 2);
        Feed(filter, 110.0, 2);
        Feed(filter, 110.0, 2);
        // Octave error for one frame
        var result = Feed(filter, 220.0, 1200);

        result.Cents.Should().BeApproximately(2, 0.01);
        result.Frequency.Should().BeApproximately(110.0, 0.01);
        result.State.Should().Be(TunerState.Sharp);
    }

    [Fact]
    public void SettleFrames_FirstReadingsAreHeldAsUnstable()
    {
        var filter = new StabilityFilter(smoothingAlpha: 1.0, stabilityFrames: 100, inTuneTolerance: 5.0,
            medianWindow: 5, onsetHoldFrames: 0, jumpThresholdCents: 10_000, jumpConfirmFrames: 1);

        Feed(filter, 110.0, -8).State.Should().Be(TunerState.Unstable);
        Feed(filter, 110.0, -8).State.Should().Be(TunerState.Unstable);
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
        Feed(filter, 113.0, 45, level: 0.3).State.Should().Be(TunerState.Unstable);
        Feed(filter, 111.5, 25, level: 0.3).State.Should().Be(TunerState.Unstable);
        Feed(filter, 108.0, -30, level: 0.3).State.Should().Be(TunerState.Unstable);
        Feed(filter, 110.8, 12, level: 0.3).State.Should().Be(TunerState.Unstable);

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
}
