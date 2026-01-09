using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;

namespace Tuner.UI.Win.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ITunerEngine _tunerEngine;
    private readonly IAudioInput _audioInput;
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
    private string _tuningInstruction = "Play a string";

    public MainViewModel(ITunerEngine tunerEngine, IAudioInput audioInput)
    {
        _tunerEngine = tunerEngine;
        _audioInput = audioInput;

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
            StatusText = "Play a string to tune";
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
            StatusText = "Play a string to tune";
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

            // Update string targets
            StringTargets.Clear();
            foreach (var target in value.Strings)
            {
                StringTargets.Add(target);
            }
        }
    }

    [RelayCommand]
    private void SelectString(StringTarget? target)
    {
        if (target != null)
        {
            TargetStringName = target.Name;
            TargetFrequency = target.Frequency;
        }
    }

    private void OnFrameReady(object? sender, TunerFrame frame)
    {
        // Update UI on UI thread
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            State = frame.State;
            SignalLevel = frame.SignalLevel;
            CentsOffset = frame.CentsOffset;
            DetectedNoteName = frame.DetectedNoteName ?? "--";

            if (frame.TargetString != null)
            {
                TargetStringName = frame.TargetString.Name;
                TargetFrequency = frame.TargetString.Frequency;
            }

            // Update tuning instruction - simple "Tune Up" / "Tune Down" / "In Tune"
            TuningInstruction = frame.State switch
            {
                TunerState.InTune => "In Tune!",
                TunerState.Flat => "Tune Up",
                TunerState.Sharp => "Tune Down",
                TunerState.TooQuiet => "Play louder",
                TunerState.Unstable => "Hold steady...",
                TunerState.Listening => "Play a string",
                _ => "Play a string"
            };

            // Update status text
            StatusText = frame.State switch
            {
                TunerState.Unknown => "Waiting for signal...",
                TunerState.Listening => "Listening...",
                TunerState.TooQuiet => "Signal too quiet",
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
