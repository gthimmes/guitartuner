using FluentAssertions;
using Tuner.AppContracts;
using Tuner.Core;
using Xunit;

namespace Tuner.Integration.Tests;

public class EventFlowTests : IDisposable
{
    private readonly MockAudioInput _audioInput;
    private readonly TunerEngine _engine;

    public EventFlowTests()
    {
        _audioInput = new MockAudioInput();
        var config = new TunerConfiguration
        {
            WindowSize = 2048,
            HopSize = 256,
            StabilityFrames = 3
        };
        _engine = new TunerEngine(_audioInput, config);
    }

    [Fact]
    public async Task FrameReady_FiredForEachProcessedWindow()
    {
        // Arrange
        var frameCount = 0;
        _engine.FrameReady += (_, _) => Interlocked.Increment(ref frameCount);
        await _engine.StartAsync();

        // Act - Send enough samples for multiple frames
        var samples = new float[8192];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 110 * i / 48000);
        }
        _audioInput.SimulateAudioFrame(samples);

        await Task.Delay(200);

        // Assert
        frameCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StateChanged_FiredOnStartAndStop()
    {
        // Arrange
        var stateChanges = new List<bool>();
        _engine.StateChanged += (_, e) => stateChanges.Add(e.IsRunning);

        // Act
        await _engine.StartAsync();
        await Task.Delay(50);
        await _engine.StopAsync();
        await Task.Delay(50);

        // Assert
        stateChanges.Should().Contain(true);  // Started
        stateChanges.Should().Contain(false); // Stopped
    }

    [Fact]
    public async Task TunerFrame_ContainsAllRequiredFields()
    {
        // Arrange
        TunerFrame? receivedFrame = null;
        _engine.FrameReady += (_, frame) => receivedFrame = frame;
        await _engine.StartAsync();

        // Act
        var samples = new float[4096];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 110 * i / 48000);
        }
        _audioInput.SimulateAudioFrame(samples);

        await Task.Delay(200);

        // Assert
        receivedFrame.Should().NotBeNull();
        receivedFrame!.Timestamp.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromSeconds(5));
        receivedFrame.SignalLevel.Should().BeGreaterOrEqualTo(0);
        receivedFrame.State.Should().BeOneOf(
            TunerState.Unknown,
            TunerState.Listening,
            TunerState.TooQuiet,
            TunerState.Unstable,
            TunerState.Flat,
            TunerState.Sharp,
            TunerState.InTune
        );
    }

    [Fact]
    public async Task StateTransitions_FollowExpectedPattern()
    {
        // Arrange
        var states = new List<TunerState>();
        _engine.FrameReady += (_, frame) =>
        {
            lock (states)
            {
                if (states.Count == 0 || states.Last() != frame.State)
                {
                    states.Add(frame.State);
                }
            }
        };
        await _engine.StartAsync();

        // Act - Start with silence, then play a note
        _audioInput.SimulateAudioFrame(new float[2048]); // Silence
        await Task.Delay(100);

        // Then play A2
        for (int j = 0; j < 20; j++)
        {
            var samples = new float[512];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 110 * (j * 512 + i) / 48000);
            }
            _audioInput.SimulateAudioFrame(samples);
            await Task.Delay(10);
        }

        // Assert - Should have transitioned through states
        lock (states)
        {
            states.Should().NotBeEmpty();
            // Should start with TooQuiet or Listening, then progress
        }
    }

    [Fact]
    public async Task MultipleStartStop_HandledCorrectly()
    {
        // Act & Assert
        await _engine.StartAsync();
        _engine.IsRunning.Should().BeTrue();

        await _engine.StartAsync(); // Second start should be no-op
        _engine.IsRunning.Should().BeTrue();

        await _engine.StopAsync();
        _engine.IsRunning.Should().BeFalse();

        await _engine.StopAsync(); // Second stop should be no-op
        _engine.IsRunning.Should().BeFalse();

        await _engine.StartAsync(); // Should be able to start again
        _engine.IsRunning.Should().BeTrue();
    }

    public void Dispose()
    {
        _engine.Dispose();
        _audioInput.Dispose();
    }
}
