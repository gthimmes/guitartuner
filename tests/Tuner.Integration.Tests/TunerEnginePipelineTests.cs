using FluentAssertions;
using Tuner.AppContracts;
using Tuner.Core;
using Xunit;

namespace Tuner.Integration.Tests;

public class TunerEnginePipelineTests : IDisposable
{
    private readonly MockAudioInput _audioInput;
    private readonly TunerEngine _engine;
    private readonly List<TunerFrame> _receivedFrames;
    private readonly SemaphoreSlim _frameSignal;

    public TunerEnginePipelineTests()
    {
        _audioInput = new MockAudioInput();
        var config = new TunerConfiguration
        {
            WindowSize = 2048,
            HopSize = 256,
            StabilityFrames = 3,
            MinConfidence = 0.7,
            MinRmsThreshold = 0.01
        };
        _engine = new TunerEngine(_audioInput, config);
        _receivedFrames = new List<TunerFrame>();
        _frameSignal = new SemaphoreSlim(0);

        _engine.FrameReady += (_, frame) =>
        {
            lock (_receivedFrames)
            {
                _receivedFrames.Add(frame);
            }
            _frameSignal.Release();
        };
    }

    [Fact]
    public async Task StartAsync_InitializesEngineAndAudio()
    {
        // Act
        await _engine.StartAsync();

        // Assert
        _engine.IsRunning.Should().BeTrue();
        _audioInput.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_StopsEngineAndAudio()
    {
        // Arrange
        await _engine.StartAsync();

        // Act
        await _engine.StopAsync();

        // Assert
        _engine.IsRunning.Should().BeFalse();
        _audioInput.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessesAudioFrame_ProducesTunerFrame()
    {
        // Arrange
        await _engine.StartAsync();

        // Act - Simulate some audio
        _audioInput.SimulateTone(110.0, 0.1); // A2 for 100ms

        // Wait for processing
        await Task.Delay(200);

        // Assert
        lock (_receivedFrames)
        {
            _receivedFrames.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task DetectsCorrectString_ForE2()
    {
        // Arrange
        await _engine.StartAsync();

        // Act - Simulate E2 (82.41 Hz)
        SimulateSustainedTone(82.41, 500);
        await WaitForFrames(5);

        // Assert
        lock (_receivedFrames)
        {
            var validFrames = _receivedFrames.Where(f => f.TargetString != null).ToList();
            validFrames.Should().NotBeEmpty();
            validFrames.Last().TargetString!.Name.Should().Be("E");
        }
    }

    [Fact]
    public async Task DetectsCorrectString_ForA2()
    {
        // Arrange
        await _engine.StartAsync();

        // Act - Simulate A2 (110 Hz)
        SimulateSustainedTone(110.0, 500);
        await WaitForFrames(5);

        // Assert
        lock (_receivedFrames)
        {
            var validFrames = _receivedFrames.Where(f => f.TargetString != null).ToList();
            validFrames.Should().NotBeEmpty();
            validFrames.Last().TargetString!.Name.Should().Be("A");
        }
    }

    [Fact]
    public async Task LowSignal_ReportsTooQuiet()
    {
        // Arrange
        await _engine.StartAsync();

        // Act - Simulate very quiet signal
        var samples = new float[2048];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.0001f * (float)Math.Sin(2 * Math.PI * 110 * i / 48000);
        }
        _audioInput.SimulateAudioFrame(samples);
        await Task.Delay(100);

        // Assert
        lock (_receivedFrames)
        {
            var quietFrames = _receivedFrames.Where(f => f.State == TunerState.TooQuiet);
            quietFrames.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task SetTuning_ChangesToDropD()
    {
        // Arrange
        await _engine.StartAsync();

        // Act
        _engine.SetTuning(TuningProfiles.DropD);

        // Simulate low D (73.42 Hz)
        SimulateSustainedTone(73.42, 500);
        await WaitForFrames(5);

        // Assert
        _engine.CurrentTuning.Name.Should().Be("Drop D");
        lock (_receivedFrames)
        {
            var validFrames = _receivedFrames.Where(f => f.TargetString != null).ToList();
            if (validFrames.Any())
            {
                validFrames.Last().TargetString!.Name.Should().Be("D");
            }
        }
    }

    [Fact]
    public async Task DeviceDisconnect_HandledGracefully()
    {
        // Arrange
        await _engine.StartAsync();
        var stateChanges = new List<TunerEngineStateEventArgs>();
        _engine.StateChanged += (_, e) => stateChanges.Add(e);

        // Act
        _audioInput.SimulateDisconnect();
        await Task.Delay(100);

        // Assert - Engine should still be in a valid state
        stateChanges.Should().NotBeEmpty();
    }

    private void SimulateSustainedTone(double frequency, int durationMs)
    {
        const int sampleRate = 48000;
        int totalSamples = (int)(sampleRate * durationMs / 1000.0);
        int chunkSize = 512;

        for (int offset = 0; offset < totalSamples; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, totalSamples - offset);
            float[] chunk = new float[remaining];

            for (int i = 0; i < remaining; i++)
            {
                chunk[i] = 0.5f * (float)Math.Sin(2 * Math.PI * frequency * (offset + i) / sampleRate);
            }

            _audioInput.SimulateAudioFrame(chunk);
            Thread.Sleep(5); // Small delay to simulate real-time
        }
    }

    private async Task WaitForFrames(int count, int timeoutMs = 2000)
    {
        for (int i = 0; i < count; i++)
        {
            if (!await _frameSignal.WaitAsync(timeoutMs))
                break;
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        _audioInput.Dispose();
        _frameSignal.Dispose();
    }
}
