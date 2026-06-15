using System.Collections.Concurrent;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;

namespace Tuner.Core;

/// <summary>
/// Main tuner engine that orchestrates pitch detection and string classification.
/// </summary>
public sealed class TunerEngine : ITunerEngine
{
    private readonly IAudioInput _audioInput;
    private readonly IPitchDetector _pitchDetector;
    private readonly StringClassifier _stringClassifier;
    private readonly StabilityFilter _stabilityFilter;
    private readonly ConcurrentQueue<float[]> _sampleQueue;
    private readonly object _lock = new();

    private TuningProfile _currentTuning;
    private TunerConfiguration _configuration;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private bool _isRunning;
    private bool _disposed;

    // Circular buffer for accumulating samples
    private float[] _sampleBuffer;
    private int _bufferWriteIndex;
    private int _samplesInBuffer;

    public TunerEngine(IAudioInput audioInput, TunerConfiguration? configuration = null)
    {
        _audioInput = audioInput ?? throw new ArgumentNullException(nameof(audioInput));
        _configuration = configuration ?? TunerConfiguration.Default;
        _configuration.Validate();

        _currentTuning = TuningProfiles.Standard;
        // Optimized for guitar: 70 Hz (below low E) to 400 Hz (above high E)
        _pitchDetector = new McLeodPitchDetector(minFrequency: 70, maxFrequency: 400);
        _stringClassifier = new StringClassifier(_configuration.MaxCentsForStringMatch, _configuration.HysteresisFrames);
        _stabilityFilter = new StabilityFilter(_configuration.SmoothingAlpha, _configuration.StabilityFrames, _configuration.InTuneTolerance);
        _sampleQueue = new ConcurrentQueue<float[]>();
        _sampleBuffer = new float[_configuration.WindowSize * 2];

        _audioInput.AudioFrameReceived += OnAudioFrameReceived;
        _audioInput.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    public bool IsRunning => _isRunning;
    public TuningProfile CurrentTuning => _currentTuning;
    public TunerConfiguration Configuration => _configuration;

    public event EventHandler<TunerFrame>? FrameReady;
    public event EventHandler<TunerEngineStateEventArgs>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isRunning)
            return;

        lock (_lock)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            ResetState();
        }

        try
        {
            await _audioInput.StartCaptureAsync(cancellationToken);

            _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = ProcessingLoop(_processingCts.Token);

            StateChanged?.Invoke(this, new TunerEngineStateEventArgs { IsRunning = true });
        }
        catch (Exception ex)
        {
            _isRunning = false;
            StateChanged?.Invoke(this, new TunerEngineStateEventArgs
            {
                IsRunning = false,
                ErrorMessage = ex.Message,
                Exception = ex
            });
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            return;

        _processingCts?.Cancel();

        try
        {
            if (_processingTask != null)
            {
                await _processingTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch (TimeoutException)
        {
            // Processing loop didn't stop in time, continue anyway
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await _audioInput.StopCaptureAsync(cancellationToken);

        lock (_lock)
        {
            _isRunning = false;
            _processingCts?.Dispose();
            _processingCts = null;
            _processingTask = null;
        }

        StateChanged?.Invoke(this, new TunerEngineStateEventArgs { IsRunning = false });
    }

    public void SetTuning(TuningProfile profile)
    {
        _currentTuning = profile ?? throw new ArgumentNullException(nameof(profile));
        _stringClassifier.Reset();
        _stabilityFilter.Reset();
    }

    public void UpdateConfiguration(TunerConfiguration configuration)
    {
        configuration.Validate();
        _configuration = configuration;

        // Resize buffer if window size changed
        if (_sampleBuffer.Length < configuration.WindowSize * 2)
        {
            _sampleBuffer = new float[configuration.WindowSize * 2];
            _bufferWriteIndex = 0;
            _samplesInBuffer = 0;
        }
    }

    private void OnAudioFrameReceived(object? sender, AudioFrameEventArgs e)
    {
        if (!_isRunning)
            return;

        // Convert to mono if needed
        float[] samples = e.Channels > 1
            ? SignalAnalyzer.StereoToMono(e.Samples)
            : e.Samples;

        _sampleQueue.Enqueue(samples);
    }

    private void OnDeviceStatusChanged(object? sender, AudioDeviceStatusEventArgs e)
    {
        if (e.Status == AudioDeviceStatus.Disconnected || e.Status == AudioDeviceStatus.Error)
        {
            StateChanged?.Invoke(this, new TunerEngineStateEventArgs
            {
                IsRunning = _isRunning,
                ErrorMessage = e.Message
            });
        }
    }

    private async Task ProcessingLoop(CancellationToken cancellationToken)
    {
        int sampleRate = _audioInput.CurrentDevice?.SampleRate ?? 48000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Process queued samples
                while (_sampleQueue.TryDequeue(out var samples))
                {
                    AddSamplesToBuffer(samples);
                }

                // Check if we have enough samples for analysis
                if (_samplesInBuffer >= _configuration.WindowSize)
                {
                    ProcessBuffer(sampleRate);
                }

                // Small delay to prevent busy-waiting
                await Task.Delay(5, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log error but continue processing
            }
        }
    }

    private void AddSamplesToBuffer(float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            _sampleBuffer[_bufferWriteIndex] = samples[i];
            _bufferWriteIndex = (_bufferWriteIndex + 1) % _sampleBuffer.Length;
            _samplesInBuffer = Math.Min(_samplesInBuffer + 1, _sampleBuffer.Length);
        }
    }

    private void ProcessBuffer(int sampleRate)
    {
        // Extract window from circular buffer
        int windowSize = _configuration.WindowSize;
        float[] window = new float[windowSize];

        int readIndex = (_bufferWriteIndex - windowSize + _sampleBuffer.Length) % _sampleBuffer.Length;
        for (int i = 0; i < windowSize; i++)
        {
            window[i] = _sampleBuffer[(readIndex + i) % _sampleBuffer.Length];
        }

        // Move buffer forward by hop size
        _samplesInBuffer -= _configuration.HopSize;

        // Calculate signal level
        double rms = SignalAnalyzer.CalculateRms(window);

        // Remove DC offset
        SignalAnalyzer.RemoveDcOffset(window);

        // NOTE: Do NOT apply a Hann (or any) window here. This detector is
        // autocorrelation/NSDF based (time domain), not FFT based. Tapering the
        // window ends shortens the effective overlap at long lags and biases the
        // interpolated peak sharp -- measured at +8 cents on low E. Windowing is
        // only appropriate ahead of an FFT, which this path does not use.

        // Detect pitch
        var pitchResult = _pitchDetector.DetectPitch(window, sampleRate);

        // Classify string
        var stringResult = pitchResult.IsValid
            ? _stringClassifier.Classify(pitchResult.Frequency, _currentTuning)
            : StringClassificationResult.Empty;

        // Apply stability filter
        var filtered = _stabilityFilter.Process(
            pitchResult.Frequency,
            stringResult.CentsOffset,
            pitchResult.Confidence,
            rms,
            _configuration.MinConfidence,
            _configuration.MinRmsThreshold
        );

        // Create output frame
        var frame = new TunerFrame
        {
            Timestamp = DateTimeOffset.Now,
            DetectedFrequency = filtered.Frequency > 0 ? filtered.Frequency : null,
            Confidence = pitchResult.Confidence,
            DetectedNoteName = filtered.Frequency > 0 ? CentsCalculator.FrequencyToNoteName(filtered.Frequency) : null,
            CentsOffset = filtered.Cents,
            TargetString = stringResult.TargetString,
            State = filtered.State,
            SignalLevel = rms
        };

        // Notify listeners
        FrameReady?.Invoke(this, frame);
    }

    private void ResetState()
    {
        _stringClassifier.Reset();
        _stabilityFilter.Reset();
        _bufferWriteIndex = 0;
        _samplesInBuffer = 0;
        while (_sampleQueue.TryDequeue(out _)) { }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TunerEngine));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _processingCts?.Cancel();
        _processingCts?.Dispose();

        _audioInput.AudioFrameReceived -= OnAudioFrameReceived;
        _audioInput.DeviceStatusChanged -= OnDeviceStatusChanged;
    }
}
