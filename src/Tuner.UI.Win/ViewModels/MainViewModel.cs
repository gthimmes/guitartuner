using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;

namespace Tuner.UI.Win.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const string IdleInstruction = "Play a string";
    private const string IdleStatus = "Play a string to tune";
    private const string ToneIdleInstruction = "Pick a string to hear its pitch";
    private const string TonePlayingInstruction = "Tune until the beating stops";

    private readonly ITunerEngine _tunerEngine;
    private readonly IAudioInput _audioInput;
    private readonly IReferenceTonePlayer _tonePlayer;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<AudioDevice> _audioDevices = new();

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private ObservableCollection<TuningProfile> _tuningProfiles = new();

    [ObservableProperty]
    private TuningProfile? _selectedTuning;

    [ObservableProperty]
    private ObservableCollection<StringTarget> _stringTargets = new();

    [ObservableProperty]
    private string? _targetStringName;

    [ObservableProperty]
    private double? _targetFrequency;

    [ObservableProperty]
    private string? _detectedNoteName;

    [ObservableProperty]
    private double _centsOffset;

    [ObservableProperty]
    private TunerState _state = TunerState.Unknown;

    [ObservableProperty]
    private double _signalLevel;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private string _tuningInstruction = IdleInstruction;

    /// <summary>
    /// False: listen to the microphone and show the gauge. True: play a
    /// reference tone for a chosen string and tune by ear instead.
    /// </summary>
    [ObservableProperty]
    private bool _isToneMode;

    /// <summary>
    /// The string whose reference tone is playing, or null when silent.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTonePlaying))]
    private StringTarget? _playingString;

    [ObservableProperty]
    private string _toneInstruction = ToneIdleInstruction;

    public bool IsTonePlaying => PlayingString != null;

    public MainViewModel(ITunerEngine tunerEngine, IAudioInput audioInput, IReferenceTonePlayer tonePlayer)
    {
        _tunerEngine = tunerEngine;
        _audioInput = audioInput;
        _tonePlayer = tonePlayer;

        // Subscribe to tuner events
        _tunerEngine.FrameReady += OnFrameReady;
        _tunerEngine.StateChanged += OnStateChanged;

        // Initialize tuning profiles
        foreach (var profile in Tuner.Core.TuningProfiles.All)
        {
            TuningProfiles.Add(profile);
        }
        SelectedTuning = TuningProfiles.FirstOrDefault();
    }

    public async Task InitializeAsync()
    {
        try
        {
            StatusText = "Loading audio devices...";

            // Enumerate audio devices
            var devices = _audioInput.ListDevices();
            AudioDevices.Clear();
            foreach (var device in devices)
            {
                AudioDevices.Add(device);
            }

            // Select default device
            SelectedDevice = AudioDevices.FirstOrDefault(d => d.IsDefault) ?? AudioDevices.FirstOrDefault();

            if (SelectedDevice != null)
            {
                await _audioInput.SetDeviceAsync(SelectedDevice.Id);
            }

            // Start tuner
            await _tunerEngine.StartAsync();
            StatusText = IdleStatus;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    public async Task ShutdownAsync()
    {
        try
        {
            StopTone();
            await _tunerEngine.StopAsync();
        }
        catch
        {
            // Ignore shutdown errors
        }
    }

    partial void OnSelectedDeviceChanged(AudioDevice? value)
    {
        if (value != null)
        {
            _ = ChangeDeviceAsync(value);
        }
    }

    private async Task ChangeDeviceAsync(AudioDevice device)
    {
        try
        {
            StatusText = "Switching device...";
            await _audioInput.SetDeviceAsync(device.Id);
            StatusText = IdleStatus;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    partial void OnSelectedTuningChanged(TuningProfile? value)
    {
        if (value != null)
        {
            _tunerEngine.SetTuning(value);
            StopTone();

            // Update string targets
            StringTargets.Clear();
            foreach (var target in value.Strings)
            {
                StringTargets.Add(target);
            }
        }
    }

    partial void OnIsToneModeChanged(bool value)
    {
        if (value)
        {
            ToneInstruction = ToneIdleInstruction;
            StatusText = "Reference tone: pick a string and match it by ear";
        }
        else
        {
            StopTone();
            ResetLiveDisplay();
            StatusText = IdleStatus;
        }
    }

    /// <summary>
    /// String button: in microphone mode picks the target string to show; in
    /// tone mode plays that string's pitch (or stops it if already playing).
    /// </summary>
    [RelayCommand]
    private void SelectString(StringTarget? target)
    {
        if (target == null)
            return;

        if (IsToneMode)
        {
            if (target.Equals(PlayingString))
                StopTone();
            else
                PlayTone(target);
            return;
        }

        TargetStringName = target.Name;
        TargetFrequency = target.Frequency;
    }

    [RelayCommand]
    private void StopTone()
    {
        try
        {
            _tonePlayer.Stop();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }

        PlayingString = null;
        ToneInstruction = ToneIdleInstruction;
        if (IsToneMode)
            StatusText = "Reference tone stopped";
    }

    private void PlayTone(StringTarget target)
    {
        try
        {
            _tonePlayer.Play(target.Frequency);
            PlayingString = target;
            ToneInstruction = TonePlayingInstruction;
            StatusText = $"Playing {target.Name} reference tone ({target.Frequency:F2} Hz)";
        }
        catch (Exception ex)
        {
            PlayingString = null;
            StatusText = $"Error: can't play tone - {ex.Message}";
        }
    }

    private void ResetLiveDisplay()
    {
        State = TunerState.Listening;
        CentsOffset = 0;
        TuningInstruction = IdleInstruction;
    }

    private void OnFrameReady(object? sender, TunerFrame frame)
    {
        // While a reference tone is playing the microphone hears the speaker,
        // so live readings are meaningless: ignore them.
        if (IsToneMode)
            return;

        // Update UI on UI thread
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (IsToneMode)
                return;

            State = frame.State;
            SignalLevel = frame.SignalLevel;
            CentsOffset = frame.CentsOffset;
            DetectedNoteName = frame.DetectedNoteName ?? "--";

            if (frame.TargetString != null)
            {
                TargetStringName = frame.TargetString.Name;
                TargetFrequency = frame.TargetString.Frequency;
            }

            // Update tuning instruction - simple "Tune Up" / "Tune Down" / "In Tune".
            // TooQuiet is the normal idle state (room noise is gated out), so it
            // reads as "waiting", not as a complaint about the signal.
            TuningInstruction = frame.State switch
            {
                TunerState.InTune => "In Tune!",
                TunerState.Flat => "Tune Up",
                TunerState.Sharp => "Tune Down",
                TunerState.Unstable => "Hold steady...",
                _ => IdleInstruction
            };

            // Update status text
            StatusText = frame.State switch
            {
                TunerState.Unknown => "Waiting for signal...",
                TunerState.Listening => "Listening...",
                TunerState.TooQuiet => "Listening...",
                TunerState.Unstable => "Signal unstable",
                TunerState.Flat => $"String {frame.TargetString?.Name ?? "?"} is flat",
                TunerState.Sharp => $"String {frame.TargetString?.Name ?? "?"} is sharp",
                TunerState.InTune => $"String {frame.TargetString?.Name ?? "?"} is in tune!",
                _ => "Ready"
            };
        });
    }

    private void OnStateChanged(object? sender, TunerEngineStateEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!e.IsRunning && e.ErrorMessage != null)
            {
                StatusText = $"Error: {e.ErrorMessage}";
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tunerEngine.FrameReady -= OnFrameReady;
        _tunerEngine.StateChanged -= OnStateChanged;
        _tunerEngine.Dispose();
    }
}
