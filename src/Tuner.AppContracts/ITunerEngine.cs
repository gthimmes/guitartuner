namespace Tuner.AppContracts;

/// <summary>
/// Main interface for the tuner engine.
/// </summary>
public interface ITunerEngine : IDisposable
{
    /// <summary>
    /// Gets whether the engine is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current tuning profile.
    /// </summary>
    TuningProfile CurrentTuning { get; }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    TunerConfiguration Configuration { get; }

    /// <summary>
    /// Event fired when a new tuner frame is available.
    /// </summary>
    event EventHandler<TunerFrame>? FrameReady;

    /// <summary>
    /// Event fired when the engine state changes (started/stopped/error).
    /// </summary>
    event EventHandler<TunerEngineStateEventArgs>? StateChanged;

    /// <summary>
    /// Starts the tuner engine.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the tuner engine.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the tuning profile.
    /// </summary>
    void SetTuning(TuningProfile profile);

    /// <summary>
    /// Updates the configuration.
    /// </summary>
    void UpdateConfiguration(TunerConfiguration configuration);
}

/// <summary>
/// Event args for tuner engine state changes.
/// </summary>
public sealed class TunerEngineStateEventArgs : EventArgs
{
    public bool IsRunning { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}
