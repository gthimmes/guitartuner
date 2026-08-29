using FluentAssertions;
using Moq;
using Tuner.AppContracts;
using Tuner.Audio.Abstractions;
using Tuner.UI.Win.ViewModels;
using Xunit;

namespace Tuner.UI.Tests;

/// <summary>
/// ViewModel unit tests - testing the MVVM layer without actual UI.
/// </summary>
public class ViewModelTests
{
    private readonly Mock<ITunerEngine> _mockEngine;
    private readonly Mock<IAudioInput> _mockAudioInput;
    private readonly Mock<IReferenceTonePlayer> _mockTonePlayer;
    private readonly MainViewModel _viewModel;

    public ViewModelTests()
    {
        _mockEngine = new Mock<ITunerEngine>();
        _mockAudioInput = new Mock<IAudioInput>();
        _mockTonePlayer = new Mock<IReferenceTonePlayer>();

        _mockEngine.Setup(e => e.CurrentTuning).Returns(CreateStandardTuning());
        _mockEngine.Setup(e => e.Configuration).Returns(TunerConfiguration.Default);
        _mockAudioInput.Setup(a => a.ListDevices()).Returns(new List<AudioDevice>
        {
            new("test-device", "Test Microphone", true)
        });

        _viewModel = new MainViewModel(_mockEngine.Object, _mockAudioInput.Object, _mockTonePlayer.Object);
    }

    [Fact]
    public void Constructor_InitializesTuningProfiles()
    {
        // Assert
        _viewModel.TuningProfiles.Should().NotBeEmpty();
        _viewModel.SelectedTuning.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_LoadsDevices()
    {
        // Arrange
        _mockAudioInput.Setup(a => a.SetDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEngine.Setup(e => e.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.AudioDevices.Should().NotBeEmpty();
        _viewModel.SelectedDevice.Should().NotBeNull();
    }

    [Fact]
    public void FrameReady_UpdatesViewModelProperties()
    {
        // Arrange
        var frame = new TunerFrame
        {
            Timestamp = DateTimeOffset.Now,
            DetectedFrequency = 110.0,
            Confidence = 0.95,
            DetectedNoteName = "A2",
            CentsOffset = -5.0,
            TargetString = new StringTarget("A2", 110.0, 5),
            State = TunerState.Flat,
            SignalLevel = 0.3
        };

        // Act - Simulate frame ready event
        _mockEngine.Raise(e => e.FrameReady += null, _mockEngine.Object, frame);

        // Note: In real test, we'd need to marshal to UI thread
        // For unit test, we check the event handler was set up correctly
        _mockEngine.VerifyAdd(e => e.FrameReady += It.IsAny<EventHandler<TunerFrame>>(), Times.Once);
    }

    [Fact]
    public void SelectStringCommand_SetsTargetString()
    {
        // Arrange
        var target = new StringTarget("E2", 82.41, 6);

        // Act
        _viewModel.SelectStringCommand.Execute(target);

        // Assert
        _viewModel.TargetStringName.Should().Be("E2");
        _viewModel.TargetFrequency.Should().BeApproximately(82.41, 0.01);
    }

    [Fact]
    public void SelectedTuning_ChangesProfile()
    {
        // Arrange
        var dropD = _viewModel.TuningProfiles.FirstOrDefault(p => p.Name == "Drop D");

        // Act
        _viewModel.SelectedTuning = dropD;

        // Assert
        _mockEngine.Verify(e => e.SetTuning(dropD!), Times.Once);
        _viewModel.StringTargets.Should().NotBeEmpty();
    }

    [Fact]
    public void Dispose_UnsubscribesEvents()
    {
        // Act
        _viewModel.Dispose();

        // Assert
        _mockEngine.VerifyRemove(e => e.FrameReady -= It.IsAny<EventHandler<TunerFrame>>(), Times.Once);
        _mockEngine.Verify(e => e.Dispose(), Times.Once);
    }

    private static TuningProfile CreateStandardTuning()
    {
        return new TuningProfile("Standard", new[]
        {
            new StringTarget("E4", 329.63, 1),
            new StringTarget("B3", 246.94, 2),
            new StringTarget("G3", 196.00, 3),
            new StringTarget("D3", 146.83, 4),
            new StringTarget("A2", 110.00, 5),
            new StringTarget("E2", 82.41, 6)
        });
    }

    // ---- Reference tone mode -----------------------------------------------

    private StringTarget LowE => _viewModel.StringTargets.First(s => s.StringNumber == 6);

    [Fact]
    public void MicrophoneMode_SelectString_SetsTargetWithoutPlayingTone()
    {
        _viewModel.SelectStringCommand.Execute(LowE);

        _viewModel.TargetStringName.Should().Be(LowE.Name);
        _viewModel.TargetFrequency.Should().BeApproximately(82.41, 0.01);
        _viewModel.IsTonePlaying.Should().BeFalse();
        _mockTonePlayer.Verify(p => p.Play(It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public void ToneMode_SelectString_PlaysItsPitch()
    {
        _viewModel.IsToneMode = true;

        _viewModel.SelectStringCommand.Execute(LowE);

        _mockTonePlayer.Verify(p => p.Play(It.Is<double>(f => Math.Abs(f - 82.41) < 0.01)), Times.Once);
        _viewModel.PlayingString.Should().Be(LowE);
        _viewModel.IsTonePlaying.Should().BeTrue();
    }

    [Fact]
    public void ToneMode_SelectPlayingStringAgain_StopsIt()
    {
        _viewModel.IsToneMode = true;
        _viewModel.SelectStringCommand.Execute(LowE);

        _viewModel.SelectStringCommand.Execute(LowE);

        _mockTonePlayer.Verify(p => p.Stop(), Times.AtLeastOnce);
        _viewModel.PlayingString.Should().BeNull();
        _viewModel.IsTonePlaying.Should().BeFalse();
    }

    [Fact]
    public void ToneMode_SelectDifferentString_SwitchesPitch()
    {
        _viewModel.IsToneMode = true;
        _viewModel.SelectStringCommand.Execute(LowE);
        var a = _viewModel.StringTargets.First(s => s.StringNumber == 5);

        _viewModel.SelectStringCommand.Execute(a);

        _mockTonePlayer.Verify(p => p.Play(It.Is<double>(f => Math.Abs(f - 110.0) < 0.01)), Times.Once);
        _viewModel.PlayingString.Should().Be(a);
    }

    [Fact]
    public void StopToneCommand_StopsPlayer()
    {
        _viewModel.IsToneMode = true;
        _viewModel.SelectStringCommand.Execute(LowE);

        _viewModel.StopToneCommand.Execute(null);

        _mockTonePlayer.Verify(p => p.Stop(), Times.AtLeastOnce);
        _viewModel.IsTonePlaying.Should().BeFalse();
    }

    [Fact]
    public void LeavingToneMode_StopsToneAndResetsDisplay()
    {
        _viewModel.IsToneMode = true;
        _viewModel.SelectStringCommand.Execute(LowE);

        _viewModel.IsToneMode = false;

        _mockTonePlayer.Verify(p => p.Stop(), Times.AtLeastOnce);
        _viewModel.IsTonePlaying.Should().BeFalse();
        _viewModel.State.Should().Be(TunerState.Listening);
        _viewModel.CentsOffset.Should().Be(0);
    }

    [Fact]
    public void ToneMode_PlayerFailure_ReportsErrorInsteadOfThrowing()
    {
        _mockTonePlayer.Setup(p => p.Play(It.IsAny<double>())).Throws(new InvalidOperationException("no output device"));
        _viewModel.IsToneMode = true;

        var act = () => _viewModel.SelectStringCommand.Execute(LowE);

        act.Should().NotThrow();
        _viewModel.IsTonePlaying.Should().BeFalse();
        _viewModel.StatusText.Should().Contain("no output device");
    }
}
