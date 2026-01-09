using FluentAssertions;
using Tuner.AppContracts;
using Xunit;

namespace Tuner.Core.Tests;

public class TuningProfilesTests
{
    [Fact]
    public void Standard_Has6Strings()
    {
        // Assert
        TuningProfiles.Standard.Strings.Should().HaveCount(6);
    }

    [Fact]
    public void Standard_HasCorrectFrequencies()
    {
        // Arrange - Using simple letter names (lowercase 'e' for high E, uppercase 'E' for low E)
        var expectedFrequencies = new[]
        {
            ("e", 329.63),
            ("B", 246.94),
            ("G", 196.00),
            ("D", 146.83),
            ("A", 110.00),
            ("E", 82.41)
        };

        // Assert
        foreach (var (name, frequency) in expectedFrequencies)
        {
            var str = TuningProfiles.Standard.Strings.FirstOrDefault(s => s.Name == name);
            str.Should().NotBeNull($"String {name} should exist");
            str!.Frequency.Should().BeApproximately(frequency, 0.1);
        }
    }

    [Fact]
    public void Standard_StringsOrderedByNumberDescending()
    {
        // Assert - strings are ordered descending so low E (string 6) appears first in UI
        var stringNumbers = TuningProfiles.Standard.Strings.Select(s => s.StringNumber).ToList();
        stringNumbers.Should().BeInDescendingOrder();
    }

    [Fact]
    public void DropD_Has6Strings()
    {
        // Assert
        TuningProfiles.DropD.Strings.Should().HaveCount(6);
    }

    [Fact]
    public void DropD_LowStringIsD()
    {
        // Arrange - First() is now the lowest string (highest string number)
        var lowString = TuningProfiles.DropD.Strings.First();

        // Assert
        lowString.Name.Should().Be("D");
        lowString.Frequency.Should().BeApproximately(73.42, 0.1);
    }

    [Fact]
    public void All_ContainsExpectedProfiles()
    {
        // Assert
        TuningProfiles.All.Should().Contain(TuningProfiles.Standard);
        TuningProfiles.All.Should().Contain(TuningProfiles.DropD);
        TuningProfiles.All.Should().Contain(TuningProfiles.HalfStepDown);
        TuningProfiles.All.Should().HaveCountGreaterOrEqualTo(4);
    }

    [Fact]
    public void CreateCustom_CreatesValidProfile()
    {
        // Act
        var custom = TuningProfiles.CreateCustom(
            "Test Tuning",
            ("A4", 440.0, 1),
            ("E4", 329.63, 2)
        );

        // Assert
        custom.Name.Should().Be("Test Tuning");
        custom.Strings.Should().HaveCount(2);
        // First() now returns highest string number (E4 with number 2)
        custom.Strings.First().Name.Should().Be("E4");
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("Drop D")]
    [InlineData("Half Step Down")]
    public void AllProfiles_HaveValidNames(string expectedName)
    {
        // Assert
        TuningProfiles.All.Should().Contain(p => p.Name == expectedName);
    }

    [Fact]
    public void AllProfiles_FrequenciesInValidRange()
    {
        // Assert
        foreach (var profile in TuningProfiles.All)
        {
            foreach (var str in profile.Strings)
            {
                str.Frequency.Should().BeGreaterThan(50, $"{profile.Name} - {str.Name}");
                str.Frequency.Should().BeLessThan(500, $"{profile.Name} - {str.Name}");
            }
        }
    }
}
