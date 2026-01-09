using Tuner.AppContracts;

namespace Tuner.Audio.Abstractions;

/// <summary>
/// Interface for audio input capture.
/// </summary>
public interface IAudioInput : IDisposable
{
    /// <summary>
    /// Gets whether audio capture is currently active.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Gets the currently selected device. Null if no device selected.
    /// </summary>
    AudioDevice? CurrentDevice { get; }

    /// <summary>
    /// Event fired when audio samples are received.
    /// </summary>
    event EventHandler<AudioFrameEventArgs>? AudioFrameReceived;

    /// <summary>
    /// Event fired when the device status changes (connected/disconnected/error).
    /// </summary>
    event EventHandler<AudioDeviceStatusEventArgs>? DeviceStatusChanged;

    /// <summary>
    /// Lists all available audio input devices.
    /// </summary>
    IReadOnlyList<AudioDevice> ListDevices();

    /// <summary>
    /// Sets the audio input device to use.
    /// </summary>
    /// <param name="deviceId">The device ID, or null for default device.</param>
    Task SetDeviceAsync(string? deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts capturing audio.
    /// </summary>
    Task StartCaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops capturing audio.
    /// </summary>
    Task StopCaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Event args for audio device status changes.
/// </summary>
public sealed class AudioDeviceStatusEventArgs : EventArgs
{
    public AudioDevice? Device { get; init; }
    public AudioDeviceStatus Status { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Status of an audio device.
/// </summary>
public enum AudioDeviceStatus
{
    Connected,
    Disconnected,
    Error,
    NotFound
}
