using NAudio.CoreAudioApi;
using NAudio.Wave;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;

namespace Tuner.Audio.Windows;

/// <summary>
/// WASAPI-based audio input implementation using NAudio.
/// </summary>
public sealed class WasapiAudioInput : IAudioInput
{
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private WasapiCapture? _capture;
    private AudioDevice? _currentDevice;
    private bool _isCapturing;
    private bool _disposed;
    private readonly object _lock = new();

    private const int PreferredSampleRate = 48000;
    private const int PreferredBitsPerSample = 32;

    public WasapiAudioInput()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
    }

    public bool IsCapturing => _isCapturing;
    public AudioDevice? CurrentDevice => _currentDevice;

    public event EventHandler<AudioFrameEventArgs>? AudioFrameReceived;
    public event EventHandler<AudioDeviceStatusEventArgs>? DeviceStatusChanged;

    public IReadOnlyList<AudioDevice> ListDevices()
    {
        ThrowIfDisposed();

        var devices = new List<AudioDevice>();

        try
        {
            var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            string defaultId = defaultDevice?.ID ?? "";

            var collection = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            foreach (var device in collection)
            {
                try
                {
                    var audioDevice = new AudioDevice(
                        device.ID,
                        device.FriendlyName,
                        device.ID == defaultId)
                    {
                        Channels = device.AudioClient.MixFormat.Channels,
                        SampleRate = device.AudioClient.MixFormat.SampleRate
                    };
                    devices.Add(audioDevice);
                }
                catch
                {
                    // Skip devices that can't be queried
                }
            }
        }
        catch
        {
            // Return empty list if enumeration fails
        }

        return devices;
    }

    public async Task SetDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        bool wasCapturing = _isCapturing;

        if (wasCapturing)
        {
            await StopCaptureAsync(cancellationToken);
        }

        lock (_lock)
        {
            DisposeCapture();

            MMDevice? device = null;

            try
            {
                if (string.IsNullOrEmpty(deviceId))
                {
                    device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                }
                else
                {
                    device = _deviceEnumerator.GetDevice(deviceId);
                }

                if (device == null)
                {
                    DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
                    {
                        Status = AudioDeviceStatus.NotFound,
                        Message = "Audio device not found"
                    });
                    return;
                }

                _currentDevice = new AudioDevice(
                    device.ID,
                    device.FriendlyName,
                    string.IsNullOrEmpty(deviceId))
                {
                    Channels = device.AudioClient.MixFormat.Channels,
                    SampleRate = device.AudioClient.MixFormat.SampleRate
                };

                // Create WASAPI capture
                _capture = new WasapiCapture(device, true, 20); // 20ms buffer for low latency
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
                {
                    Device = _currentDevice,
                    Status = AudioDeviceStatus.Connected
                });
            }
            catch (Exception ex)
            {
                DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
                {
                    Status = AudioDeviceStatus.Error,
                    Message = ex.Message
                });
                throw;
            }
        }

        if (wasCapturing)
        {
            await StartCaptureAsync(cancellationToken);
        }
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_isCapturing)
                return Task.CompletedTask;

            if (_capture == null)
            {
                // Initialize with default device if not set
                var task = SetDeviceAsync(null, cancellationToken);
                task.Wait(cancellationToken);

                if (_capture == null)
                {
                    throw new InvalidOperationException("No audio capture device available");
                }
            }

            try
            {
                _capture.StartRecording();
                _isCapturing = true;
            }
            catch (Exception ex)
            {
                DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
                {
                    Device = _currentDevice,
                    Status = AudioDeviceStatus.Error,
                    Message = ex.Message
                });
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isCapturing || _capture == null)
                return Task.CompletedTask;

            try
            {
                _capture.StopRecording();
            }
            catch
            {
                // Ignore errors during stop
            }

            _isCapturing = false;
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _capture == null)
            return;

        try
        {
            var format = _capture.WaveFormat;
            float[] samples = ConvertToFloat(e.Buffer, e.BytesRecorded, format);

            AudioFrameReceived?.Invoke(this, new AudioFrameEventArgs(
                samples,
                format.SampleRate,
                format.Channels));
        }
        catch
        {
            // Ignore conversion errors
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;

        if (e.Exception != null)
        {
            DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
            {
                Device = _currentDevice,
                Status = AudioDeviceStatus.Error,
                Message = e.Exception.Message
            });
        }
        else
        {
            DeviceStatusChanged?.Invoke(this, new AudioDeviceStatusEventArgs
            {
                Device = _currentDevice,
                Status = AudioDeviceStatus.Disconnected
            });
        }
    }

    private static float[] ConvertToFloat(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8;
        int sampleCount = bytesRecorded / bytesPerSample;
        float[] samples = new float[sampleCount];

        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            // 32-bit float
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = BitConverter.ToSingle(buffer, i * 4);
            }
        }
        else if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            if (format.BitsPerSample == 16)
            {
                // 16-bit PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    samples[i] = sample / 32768f;
                }
            }
            else if (format.BitsPerSample == 24)
            {
                // 24-bit PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int sample = (buffer[i * 3 + 2] << 16) | (buffer[i * 3 + 1] << 8) | buffer[i * 3];
                    if ((sample & 0x800000) != 0)
                        sample |= unchecked((int)0xFF000000); // Sign extend
                    samples[i] = sample / 8388608f;
                }
            }
            else if (format.BitsPerSample == 32)
            {
                // 32-bit PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    int sample = BitConverter.ToInt32(buffer, i * 4);
                    samples[i] = sample / 2147483648f;
                }
            }
        }
        else if (format.Encoding == WaveFormatEncoding.Extensible)
        {
            // Handle extensible format (typically 32-bit float)
            var waveFormatEx = format as WaveFormatExtensible;
            // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT GUID
            var floatSubFormat = new Guid("00000003-0000-0010-8000-00aa00389b71");
            if (waveFormatEx?.SubFormat == floatSubFormat || format.BitsPerSample == 32)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = BitConverter.ToSingle(buffer, i * 4);
                }
            }
        }

        return samples;
    }

    private void DisposeCapture()
    {
        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;

            try
            {
                if (_isCapturing)
                {
                    _capture.StopRecording();
                }
                _capture.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            _capture = null;
            _isCapturing = false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WasapiAudioInput));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            DisposeCapture();
            _deviceEnumerator.Dispose();
        }
    }
}
