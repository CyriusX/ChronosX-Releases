using FluentAssertions;
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
