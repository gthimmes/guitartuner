using NAudio.Wave;
using Tuner.Audio.Abstractions;
using Tuner.Core;

namespace Tuner.Audio.Windows;

/// <summary>
/// Plays a <see cref="ReferenceToneSynthesizer"/> tone through the default
/// Windows output device using NAudio's event-driven WaveOut.
/// </summary>
public sealed class WaveOutReferenceTonePlayer : IReferenceTonePlayer
{
    private const int SampleRate = 44100;
    private const int LatencyMs = 120;
    /// <summary>Time to let the fade-out finish before the output device is paused.</summary>
    private const int StopDelayMs = 250;

    private readonly object _lock = new();
    private readonly ReferenceToneSynthesizer _synth = new(SampleRate);
    private WaveOutEvent? _output;
    private bool _disposed;

    public bool IsPlaying { get; private set; }

    public double? Frequency { get; private set; }

    public void Play(double frequency)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _synth.Frequency = frequency;
            _synth.Start();
            Frequency = frequency;
            IsPlaying = true;

            _output ??= CreateOutput();
            if (_output.PlaybackState != PlaybackState.Playing)
                _output.Play();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsPlaying)
                return;

            // Fade out (the synth ramps to silence), then release the device
            // once the tail has actually been played.
            _synth.Stop();
            IsPlaying = false;
            Frequency = null;
        }

        _ = Task.Delay(StopDelayMs).ContinueWith(_ =>
        {
            lock (_lock)
            {
                if (!IsPlaying && !_disposed && _synth.IsSilent)
                    _output?.Stop();
            }
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            IsPlaying = false;
            Frequency = null;

            _output?.Stop();
            _output?.Dispose();
            _output = null;
        }
    }

    private WaveOutEvent CreateOutput()
    {
        var output = new WaveOutEvent { DesiredLatency = LatencyMs };
        output.Init(new SynthSampleProvider(_synth));
        return output;
    }

    private sealed class SynthSampleProvider : ISampleProvider
    {
        private readonly ReferenceToneSynthesizer _synth;

        public SynthSampleProvider(ReferenceToneSynthesizer synth)
        {
            _synth = synth;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(synth.SampleRate, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count) => _synth.Read(buffer, offset, count);
    }
}
