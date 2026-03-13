using FluentAssertions;
using FluentValidation;
using System.Text.Json;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Application.Policies.Validators;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.Policies;

/// <summary>
/// Unit tests for OrgPolicy entity
/// </summary>
public class OrgPolicyTests
{
    [Fact]
    public void OrgPolicy_Create_CreatesValidPolicyWithDefaults()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Act
        var policy = OrgPolicy.Create(orgId);

        // Assert
        policy.Should().NotBeNull();
        policy.Id.Should().NotBe(Guid.Empty);
        policy.OrgId.Should().Be(orgId);
        policy.Version.Should().Be(1);
        policy.IdleThresholdSeconds.Should().Be(180);
        policy.RetentionDays.Should().Be(90);
        policy.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void OrgPolicy_GetWorkHours_ReturnsValidConfig()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var policy = OrgPolicy.Create(orgId);

        // Act
        var workHours = policy.GetWorkHours();

        // Assert
        workHours.Should().NotBeNull();
        workHours!.Timezone.Should().Be("America/Sao_Paulo");
        workHours.Days.Should().Contain(new[] { "monday", "tuesday", "wednesday", "thursday", "friday" });
        workHours.StartTime.Should().Be("08:00");
        workHours.EndTime.Should().Be("18:00");
    }

    [Fact]
    public void OrgPolicy_GetAppExclusions_ReturnsEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var policy = OrgPolicy.Create(orgId);

        // Act
        var exclusions = policy.GetAppExclusions();

        // Assert
        exclusions.Should().NotBeNull();
        exclusions.Should().BeEmpty();
    }

    [Fact]
    public void OrgPolicy_Update_IncrementsVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var policy = OrgPolicy.Create(orgId);
        var initialVersion = policy.Version;

        // Act
        policy.Update(null, null, 300, 180);

        // Assert
        policy.Version.Should().Be(initialVersion + 1);
        policy.UpdatedAt.Should().NotBeNull();
        policy.IdleThresholdSeconds.Should().Be(300);
        policy.RetentionDays.Should().Be(180);
    }
}

/// <summary>
/// Tests for WorkHoursConfig
/// </summary>
public class WorkHoursConfigTests
{
    [Fact]
    public void WorkHoursConfig_IsWithinWindow_ReturnsTrueForWorkDay()
    {
        // Arrange
        var config = new WorkHoursConfig
        {
            Timezone = "America/Sao_Paulo",
            Days = new List<string> { "monday", "tuesday", "wednesday", "thursday", "friday" },
            StartTime = "08:00",
            EndTime = "18:00"
        };

        // Monday at 10:00 AM Sao Paulo time
        var mondayMorning = new DateTimeOffset(2024, 1, 8, 13, 0, 0, TimeSpan.Zero); // UTC 13:00 = 10:00 BRT

        // Act
        var result = config.IsWithinWindow(mondayMorning);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void WorkHoursConfig_IsWithinWindow_ReturnsFalseForWeekend()
    {
        // Arrange
        var config = new WorkHoursConfig
        {
            Timezone = "America/Sao_Paulo",
            Days = new List<string> { "monday", "tuesday", "wednesday", "thursday", "friday" },
            StartTime = "08:00",
            EndTime = "18:00"
        };

        // Saturday at 10:00 AM Sao Paulo time
        var saturdayMorning = new DateTimeOffset(2024, 1, 6, 13, 0, 0, TimeSpan.Zero); // UTC 13:00 = 10:00 BRT

        // Act
        var result = config.IsWithinWindow(saturdayMorning);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void WorkHoursConfig_IsWithinWindow_ReturnsFalseForAfterHours()
    {
        // Arrange
        var config = new WorkHoursConfig
        {
            Timezone = "America/Sao_Paulo",
            Days = new List<string> { "monday", "tuesday", "wednesday", "thursday", "friday" },
            StartTime = "08:00",
            EndTime = "18:00"
        };

        // Monday at 8:00 PM Sao Paulo time
        var mondayNight = new DateTimeOffset(2024, 1, 8, 23, 0, 0, TimeSpan.Zero); // UTC 23:00 = 20:00 BRT

        // Act
        var result = config.IsWithinWindow(mondayNight);

        // Assert
        result.Should().BeFalse();
    }
}

/// <summary>
/// Tests for FocusModeConfig
/// </summary>
public class FocusModeConfigTests
{
    [Fact]
    public void OrgPolicy_GetFocusMode_ReturnsValidConfig_WhenCreated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var policy = OrgPolicy.Create(orgId);

        // Act
        var focusMode = policy.GetFocusMode();

        // Assert
        focusMode.Should().NotBeNull();
        focusMode!.Enabled.Should().BeFalse();
        focusMode.Mode.Should().Be("none");
        focusMode.AllowUserOverride.Should().BeTrue();
        focusMode.Pomodoro.Should().NotBeNull();
        focusMode.Pomodoro!.FocusMinutes.Should().Be(25);
        focusMode.Pomodoro.ShortBreakMinutes.Should().Be(5);
        focusMode.Pomodoro.LongBreakMinutes.Should().Be(15);
        focusMode.Pomodoro.CyclesBeforeLongBreak.Should().Be(4);
        focusMode.Ultradian.Should().NotBeNull();
        focusMode.Ultradian!.FocusMinutes.Should().Be(90);
        focusMode.Ultradian.BreakMinutes.Should().Be(20);
    }

    [Fact]
    public void OrgPolicy_Update_WithFocusMode_UpdatesFocusMode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var policy = OrgPolicy.Create(orgId);

        var newFocusMode = new FocusModeConfig
        {
            Enabled = true,
            Mode = "pomodoro",
            AllowUserOverride = false,
            Pomodoro = new PomodoroConfig
            {
                FocusMinutes = 30,
                ShortBreakMinutes = 10,
                LongBreakMinutes = 20,
                CyclesBeforeLongBreak = 3
            },
            Ultradian = new UltradianConfig
            {
                FocusMinutes = 120,
                BreakMinutes = 30
            }
        };

        var focusModeJson = JsonSerializer.Serialize(newFocusMode, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        policy.Update(null, null, null, null, focusModeJson);

        // Assert
        var updatedFocusMode = policy.GetFocusMode();
        updatedFocusMode.Should().NotBeNull();
        updatedFocusMode!.Enabled.Should().BeTrue();
        updatedFocusMode.Mode.Should().Be("pomodoro");
        updatedFocusMode.AllowUserOverride.Should().BeFalse();
        updatedFocusMode.Pomodoro!.FocusMinutes.Should().Be(30);
        updatedFocusMode.Ultradian!.FocusMinutes.Should().Be(120);
    }
}

/// <summary>
/// Tests for FocusModeDtoValidator
/// </summary>
public class FocusModeDtoValidatorTests
{
    private readonly FocusModeDtoValidator _validator = new();

    [Fact]
    public void FocusModeDtoValidator_ValidMode_Passes()
    {
        // Arrange
        var dto = new FocusModeDto
        {
            Enabled = true,
            Mode = "pomodoro"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FocusModeDtoValidator_InvalidMode_Fails()
    {
        // Arrange
        var dto = new FocusModeDto
        {
            Mode = "invalid"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Mode must be one of"));
    }

    [Fact]
    public void FocusModeDtoValidator_EnabledWithNoneMode_Fails()
    {
        // Arrange
        var dto = new FocusModeDto
        {
            Enabled = true,
            Mode = "none"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Mode cannot be 'none' when focus mode is enabled"));
    }

    [Fact]
    public void FocusModeDtoValidator_DisabledWithNoneMode_Passes()
    {
        // Arrange
        var dto = new FocusModeDto
        {
            Enabled = false,
            Mode = "none"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

/// <summary>
/// Tests for PomodoroConfigDtoValidator
/// </summary>
public class PomodoroConfigDtoValidatorTests
{
    private readonly PomodoroConfigDtoValidator _validator = new();

    [Theory]
    [InlineData(10, true)]
    [InlineData(25, true)]
    [InlineData(180, true)]
    [InlineData(9, false)]
    [InlineData(181, false)]
    public void PomodoroConfigDtoValidator_FocusMinutesRange(int minutes, bool expectedValid)
    {
        // Arrange
        var dto = new PomodoroConfigDto { FocusMinutes = minutes };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(4, false)]
    [InlineData(61, false)]
    public void PomodoroConfigDtoValidator_BreakMinutesRange(int minutes, bool expectedValid)
    {
        // Arrange
        var dto = new PomodoroConfigDto { ShortBreakMinutes = minutes };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(1, false)]
    [InlineData(9, false)]
    public void PomodoroConfigDtoValidator_CyclesBeforeLongBreakRange(int cycles, bool expectedValid)
    {
        // Arrange
        var dto = new PomodoroConfigDto { CyclesBeforeLongBreak = cycles };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}

/// <summary>
/// Tests for UltradianConfigDtoValidator
/// </summary>
public class UltradianConfigDtoValidatorTests
{
    private readonly UltradianConfigDtoValidator _validator = new();

    [Theory]
    [InlineData(10, true)]
    [InlineData(90, true)]
    [InlineData(180, true)]
    [InlineData(9, false)]
    [InlineData(181, false)]
    public void UltradianConfigDtoValidator_FocusMinutesRange(int minutes, bool expectedValid)
    {
        // Arrange
        var dto = new UltradianConfigDto { FocusMinutes = minutes };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(20, true)]
    [InlineData(60, true)]
    [InlineData(4, false)]
    [InlineData(61, false)]
    public void UltradianConfigDtoValidator_BreakMinutesRange(int minutes, bool expectedValid)
    {
        // Arrange
        var dto = new UltradianConfigDto { BreakMinutes = minutes };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}
