using FluentAssertions;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Agent.Tests.FocusMode;

/// <summary>
/// Unit tests for FocusModePolicy and related value objects
/// </summary>
public class FocusModePolicyTests
{
    #region PomodoroConfig

    [Fact]
    public void PomodoroConfig_Default_ShouldHaveStandardValues()
    {
        // Arrange & Act
        var config = PomodoroConfig.Default;

        // Assert
        config.FocusMinutes.Should().Be(25);
        config.ShortBreakMinutes.Should().Be(5);
        config.LongBreakMinutes.Should().Be(15);
        config.CyclesBeforeLongBreak.Should().Be(4);
    }

    [Theory]
    [InlineData(9, 5, 15, 4)]   // Focus too short
    [InlineData(181, 5, 15, 4)] // Focus too long
    [InlineData(25, 4, 15, 4)]  // Short break too short
    [InlineData(25, 61, 15, 4)] // Short break too long
    [InlineData(25, 5, 4, 4)]   // Long break too short
    [InlineData(25, 5, 61, 4)]  // Long break too long
    [InlineData(25, 5, 15, 1)]  // Cycles too few
    [InlineData(25, 5, 15, 9)]  // Cycles too many
    public void PomodoroConfig_InvalidValues_ShouldThrow(
        int focusMinutes, int shortBreak, int longBreak, int cycles)
    {
        // Act
        var act = () => new PomodoroConfig(focusMinutes, shortBreak, longBreak, cycles);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PomodoroConfig_ValidCustomValues_ShouldSucceed()
    {
        // Arrange & Act
        var config = new PomodoroConfig(50, 10, 30, 3);

        // Assert
        config.FocusMinutes.Should().Be(50);
        config.ShortBreakMinutes.Should().Be(10);
        config.LongBreakMinutes.Should().Be(30);
        config.CyclesBeforeLongBreak.Should().Be(3);
    }

    [Fact]
    public void PomodoroConfig_Equality_ShouldWork()
    {
        // Arrange
        var config1 = new PomodoroConfig(30, 10, 20, 4);
        var config2 = new PomodoroConfig(30, 10, 20, 4);
        var config3 = new PomodoroConfig(25, 5, 15, 4);

        // Assert
        config1.Should().Be(config2);
        config1.Should().NotBe(config3);
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region UltradianConfig

    [Fact]
    public void UltradianConfig_Default_ShouldHaveStandardValues()
    {
        // Arrange & Act
        var config = UltradianConfig.Default;

        // Assert
        config.FocusMinutes.Should().Be(90);
        config.BreakMinutes.Should().Be(20);
    }

    [Theory]
    [InlineData(9, 20)]   // Focus too short
    [InlineData(181, 20)] // Focus too long
    [InlineData(90, 4)]   // Break too short
    [InlineData(90, 61)]  // Break too long
    public void UltradianConfig_InvalidValues_ShouldThrow(int focusMinutes, int breakMinutes)
    {
        // Act
        var act = () => new UltradianConfig(focusMinutes, breakMinutes);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UltradianConfig_ValidCustomValues_ShouldSucceed()
    {
        // Arrange & Act
        var config = new UltradianConfig(120, 30);

        // Assert
        config.FocusMinutes.Should().Be(120);
        config.BreakMinutes.Should().Be(30);
    }

    [Fact]
    public void UltradianConfig_Equality_ShouldWork()
    {
        // Arrange
        var config1 = new UltradianConfig(90, 20);
        var config2 = new UltradianConfig(90, 20);
        var config3 = new UltradianConfig(60, 15);

        // Assert
        config1.Should().Be(config2);
        config1.Should().NotBe(config3);
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region FocusModePolicy

    [Fact]
    public void FocusModePolicy_Disabled_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var policy = FocusModePolicy.Disabled;

        // Assert
        policy.Enabled.Should().BeFalse();
        policy.Mode.Should().Be(FocusModeType.None);
        policy.AllowUserOverride.Should().BeTrue();
    }

    [Fact]
    public void FocusModePolicy_DefaultPomodoro_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var policy = FocusModePolicy.DefaultPomodoro;

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.Mode.Should().Be(FocusModeType.Pomodoro);
        policy.AllowUserOverride.Should().BeTrue();
        policy.Pomodoro.Should().NotBeNull();
        policy.Pomodoro!.FocusMinutes.Should().Be(25);
    }

    [Fact]
    public void FocusModePolicy_DefaultUltradian_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var policy = FocusModePolicy.DefaultUltradian;

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.Mode.Should().Be(FocusModeType.Ultradian);
        policy.AllowUserOverride.Should().BeTrue();
        policy.Ultradian.Should().NotBeNull();
        policy.Ultradian!.FocusMinutes.Should().Be(90);
    }

    [Fact]
    public void FocusModePolicy_GetFocusMinutes_Pomodoro_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new PomodoroConfig(45, 10, 20, 4);
        var policy = new FocusModePolicy(true, FocusModeType.Pomodoro, true, config, null);

        // Act
        var minutes = policy.GetFocusMinutes();

        // Assert
        minutes.Should().Be(45);
    }

    [Fact]
    public void FocusModePolicy_GetFocusMinutes_Ultradian_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new UltradianConfig(100, 25);
        var policy = new FocusModePolicy(true, FocusModeType.Ultradian, true, null, config);

        // Act
        var minutes = policy.GetFocusMinutes();

        // Assert
        minutes.Should().Be(100);
    }

    [Fact]
    public void FocusModePolicy_GetFocusMinutes_None_ShouldReturnZero()
    {
        // Arrange
        var policy = FocusModePolicy.Disabled;

        // Act
        var minutes = policy.GetFocusMinutes();

        // Assert
        minutes.Should().Be(0);
    }

    [Fact]
    public void FocusModePolicy_GetBreakMinutes_PomodoroShortBreak_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new PomodoroConfig(25, 7, 20, 4);
        var policy = new FocusModePolicy(true, FocusModeType.Pomodoro, true, config, null);

        // Act
        var minutes = policy.GetBreakMinutes(BreakType.Short);

        // Assert
        minutes.Should().Be(7);
    }

    [Fact]
    public void FocusModePolicy_GetBreakMinutes_PomodoroLongBreak_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new PomodoroConfig(25, 5, 25, 4);
        var policy = new FocusModePolicy(true, FocusModeType.Pomodoro, true, config, null);

        // Act
        var minutes = policy.GetBreakMinutes(BreakType.Long);

        // Assert
        minutes.Should().Be(25);
    }

    [Fact]
    public void FocusModePolicy_GetBreakMinutes_Ultradian_ShouldReturnSameForBothTypes()
    {
        // Arrange
        var config = new UltradianConfig(90, 25);
        var policy = new FocusModePolicy(true, FocusModeType.Ultradian, true, null, config);

        // Act
        var shortBreak = policy.GetBreakMinutes(BreakType.Short);
        var longBreak = policy.GetBreakMinutes(BreakType.Long);

        // Assert
        shortBreak.Should().Be(25);
        longBreak.Should().Be(25);
    }

    [Fact]
    public void FocusModePolicy_GetCyclesBeforeLongBreak_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new PomodoroConfig(25, 5, 15, 3);
        var policy = new FocusModePolicy(true, FocusModeType.Pomodoro, true, config, null);

        // Act
        var cycles = policy.GetCyclesBeforeLongBreak();

        // Assert
        cycles.Should().Be(3);
    }

    [Fact]
    public void FocusModePolicy_GetCyclesBeforeLongBreak_NoConfig_ShouldReturnDefault()
    {
        // Arrange
        var policy = new FocusModePolicy(true, FocusModeType.Pomodoro, true, null, null);

        // Act
        var cycles = policy.GetCyclesBeforeLongBreak();

        // Assert
        cycles.Should().Be(4); // Default value
    }

    [Fact]
    public void FocusModePolicy_Equality_ShouldWork()
    {
        // Arrange
        var policy1 = new FocusModePolicy(
            true, FocusModeType.Pomodoro, true,
            new PomodoroConfig(30, 10, 20, 4), null);
        var policy2 = new FocusModePolicy(
            true, FocusModeType.Pomodoro, true,
            new PomodoroConfig(30, 10, 20, 4), null);
        var policy3 = new FocusModePolicy(
            true, FocusModeType.Pomodoro, false, // Different allowUserOverride
            new PomodoroConfig(30, 10, 20, 4), null);

        // Assert
        policy1.Should().Be(policy2);
        policy1.Should().NotBe(policy3);
        policy1.GetHashCode().Should().Be(policy2.GetHashCode());
    }

    #endregion
}
