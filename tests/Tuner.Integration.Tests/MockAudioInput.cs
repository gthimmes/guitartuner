using Tuner.AppContracts;
using Tuner.Audio.Abstractions;

namespace Tuner.Integration.Tests;

/// <summary>
/// Mock audio input for testing the tuner engine without hardware.
/// </summary>
public class MockAudioInput : IAudioInput
{
    private readonly List<AudioDevice> _devices;
    private bool _isCapturing;
    private bool _disposed;

    public MockAudioInput()
    {
        _devices = new List<AudioDevice>
        {
            new("mock-device-1", "Mock Microphone", true)
            {
                Channels = 1,
                SampleRate = 48000
            }
        };
    }

    public bool IsCapturing => _isCapturing;
    public AudioDevice? CurrentDevice { get; private set; }

    public event EventHandler<AudioFrameEventArgs>? AudioFrameReceived;
    public event EventHandler<AudioDeviceStatusEventArgs>? DeviceStatusChanged;

    public IReadOnlyList<AudioDevice> ListDevices() => _devices;

    public Task SetDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        CurrentDevice = _devices.FirstOrDefault(d => deviceId == null || d.Id == deviceId);

        if (CurrentDevice != null)
        {
            DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
            {
                Device = CurrentDevice,
                Status = AudioDeviceStatus.Connected
            });
        }

        return Task.CompletedTask;
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        _isCapturing = true;
        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        _isCapturing = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates receiving audio samples.
    /// </summary>
    public void SimulateAudioFrame(float[] samples, int sampleRate = 48000, int channels = 1)
    {
        if (!_isCapturing) return;

        AudioFrameReceived?.Invoke(this, new AudioFrameEventArgs(samples, sampleRate, channels));
    }

    /// <summary>
    /// Simulates a pure sine wave being captured.
    /// </summary>
    public void SimulateTone(double frequency, double durationSeconds, int sampleRate = 48000)
    {
        int sampleCount = (int)(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }

        // Send in chunks to simulate real-time capture
        int chunkSize = 512;
        for (int offset = 0; offset < sampleCount; offset += chunkSize)
        {
            int remaining = Math.Min(chunkSize, sampleCount - offset);
            float[] chunk = new float[remaining];
            Array.Copy(samples, offset, chunk, 0, remaining);
            SimulateAudioFrame(chunk, sampleRate, 1);
        }
    }

    /// <summary>
    /// Simulates device disconnect.
    /// </summary>
    public void SimulateDisconnect()
    {
        _isCapturing = false;
        DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
        {
            Device = CurrentDevice,
            Status = AudioDeviceStatus.Disconnected
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isCapturing = false;
    }
}
