using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.WeeklyReport;

public sealed class WeeklyReportScheduleTests
{
    [Fact]
    public void Create_SetsAllPropertiesCorrectly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var timeOfDay = new TimeOnly(9, 30);

        var schedule = WeeklyReportSchedule.Create(userId, orgId, 1, timeOfDay);

        schedule.Id.Should().NotBe(Guid.Empty);
        schedule.UserId.Should().Be(userId);
        schedule.OrgId.Should().Be(orgId);
        schedule.DayOfWeek.Should().Be(1);
        schedule.TimeOfDay.Should().Be(timeOfDay);
        schedule.IsEnabled.Should().BeTrue();
        schedule.PreferencesJson.Should().Be("{}");
        schedule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithPreferencesJson_SetsPreferences()
    {
        var prefsJson = """{"includeTeamComparison":true,"includeDifficultyAnalysis":false}""";

        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 3,
            new TimeOnly(14, 0), prefsJson);

        schedule.PreferencesJson.Should().Be(prefsJson);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(100)]
    public void Create_InvalidDayOfWeek_Throws(int invalidDay)
    {
        var act = () => WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), invalidDay, new TimeOnly(9, 0));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("dayOfWeek");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Create_ValidDays_DoesNotThrow(int day)
    {
        var act = () => WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), day, new TimeOnly(9, 0));

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_ChangesDayTimeAndPreferences()
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, new TimeOnly(9, 0));

        var newPrefs = """{"includeTeamComparison":false}""";
        schedule.Update(5, new TimeOnly(17, 30), newPrefs);

        schedule.DayOfWeek.Should().Be(5);
        schedule.TimeOfDay.Should().Be(new TimeOnly(17, 30));
        schedule.PreferencesJson.Should().Be(newPrefs);
        schedule.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_WithoutPreferences_DoesNotChangeExisting()
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, new TimeOnly(9, 0),
            """{"original":true}""");

        schedule.Update(3, new TimeOnly(10, 0));

        schedule.PreferencesJson.Should().Be("""{"original":true}""");
    }

    [Fact]
    public void Update_InvalidDayOfWeek_Throws()
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, new TimeOnly(9, 0));

        var act = () => schedule.Update(8, new TimeOnly(10, 0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Enable_SetsIsEnabledTrue()
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, new TimeOnly(9, 0));
        schedule.Disable();

        schedule.IsEnabled.Should().BeFalse();
        schedule.Enable();
        schedule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, new TimeOnly(9, 0));

        schedule.Disable();
        schedule.IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, DayOfWeek.Sunday)]
    [InlineData(1, DayOfWeek.Monday)]
    [InlineData(2, DayOfWeek.Tuesday)]
    [InlineData(3, DayOfWeek.Wednesday)]
    [InlineData(4, DayOfWeek.Thursday)]
    [InlineData(5, DayOfWeek.Friday)]
    [InlineData(6, DayOfWeek.Saturday)]
    public void GetSystemDayOfWeek_ReturnsCorrectMapping(int dayValue, DayOfWeek expected)
    {
        var schedule = WeeklyReportSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), dayValue, new TimeOnly(9, 0));

        schedule.GetSystemDayOfWeek().Should().Be(expected);
    }
}
