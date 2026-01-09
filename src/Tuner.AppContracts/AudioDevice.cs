namespace Tuner.AppContracts;

/// <summary>
/// Represents an audio input device.
/// </summary>
public sealed class AudioDevice
{
    /// <summary>
    /// Gets the unique identifier for this device.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the display name of the device.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this is the system default device.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Gets the number of input channels.
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; init; }

    public AudioDevice(string id, string name, bool isDefault = false)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsDefault = isDefault;
    }

    public override string ToString() => IsDefault ? $"{Name} (Default)" : Name;

    public override bool Equals(object? obj) =>
        obj is AudioDevice other && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}
