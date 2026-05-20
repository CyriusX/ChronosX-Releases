using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;
using DailyFocusScore = TimeTrack.Backend.Domain.Entities.DailyFocusScore;

namespace TimeTrack.Backend.Tests.Jobs;

/// <summary>
/// Unit tests for WeeklyEmailReportJob
/// </summary>
public class WeeklyEmailReportJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<IAIService> _aiServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<WeeklyEmailReportJob>> _loggerMock;
    private readonly WeeklyEmailReportJob _job;

    public WeeklyEmailReportJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TimeTrackDbContext(options, null);
        _aiServiceMock = new Mock<IAIService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<WeeklyEmailReportJob>>();

        _job = new WeeklyEmailReportJob(
            _context,
            new WeeklyReportDataCollector(_context),
            _aiServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSchedules_ReturnsEarly()
    {
        // Arrange - no schedules in database

        // Act
        await _job.ExecuteAsync();

        // Assert
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSchedulesExist_ButDifferentDayOfWeek_DoesNotProcess()
    {
        // Arrange
        var org = CreateTestOrganization();
        var user = CreateTestUser(org.Id);
        var schedule = CreateTestSchedule(user.Id, org.Id, dayOfWeek: 0); // Sunday
        _context.Organizations.Add(org);
        _context.Users.Add(user);
        _context.WeeklyReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act - job runs on Monday (day 1)
        await _job.ExecuteAsync();

        // Assert - should not process since schedule is for Sunday
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleMatchesCurrentDay_ProcessesSuccessfully()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user = CreateTestUser(org.Id);
        var schedule = CreateTestSchedule(user.Id, org.Id, currentDayOfWeek, currentHour);

        // Add focus score data to meet the minimum 1 hour requirement
        var focusScore = CreateTestFocusScore(user.Id, org.Id);

        _context.Organizations.Add(org);
        _context.Users.Add(user);
        _context.WeeklyReportSchedules.Add(schedule);
        _context.DailyFocusScores.Add(focusScore);
        await _context.SaveChangesAsync();

        var reportHtml = "<html>Relatorio Semanal</html>";
        _aiServiceMock
            .Setup(x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportHtml);

        // Act
        await _job.ExecuteAsync();

        // Assert
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                org.Id,
                user.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                user.Email,
                user.DisplayName,
                reportHtml,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleIsDisabled_DoesNotProcess()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user = CreateTestUser(org.Id);
        var schedule = CreateTestSchedule(user.Id, org.Id, currentDayOfWeek, currentHour);
        schedule.Disable();
        _context.Organizations.Add(org);
        _context.Users.Add(user);
        _context.WeeklyReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserHasInsufficientData_SkipsReport()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user = CreateTestUser(org.Id);
        var schedule = CreateTestSchedule(user.Id, org.Id, currentDayOfWeek, currentHour);
        _context.Organizations.Add(org);
        _context.Users.Add(user);
        _context.WeeklyReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act - no activity data, so TotalActiveHours < 1
        await _job.ExecuteAsync();

        // Assert
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_DoesNotSendEmail()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user = CreateTestUser(org.Id);
        var schedule = CreateTestSchedule(user.Id, org.Id, currentDayOfWeek, currentHour);
        _context.Organizations.Add(org);
        // User NOT added to context
        _context.WeeklyReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleSchedules_ProcessesAll()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user1 = CreateTestUser(org.Id, "user1@test.com", "User 1");
        var user2 = CreateTestUser(org.Id, "user2@test.com", "User 2");
        var schedule1 = CreateTestSchedule(user1.Id, org.Id, currentDayOfWeek, currentHour);
        var schedule2 = CreateTestSchedule(user2.Id, org.Id, currentDayOfWeek, currentHour);

        // Add focus score data for both users
        var focusScore1 = CreateTestFocusScore(user1.Id, org.Id);
        var focusScore2 = CreateTestFocusScore(user2.Id, org.Id);

        _context.Organizations.Add(org);
        _context.Users.AddRange(user1, user2);
        _context.WeeklyReportSchedules.AddRange(schedule1, schedule2);
        _context.DailyFocusScores.AddRange(focusScore1, focusScore2);
        await _context.SaveChangesAsync();

        var reportHtml = "<html>Relatorio Semanal</html>";
        _aiServiceMock
            .Setup(x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportHtml);

        // Act
        await _job.ExecuteAsync();

        // Assert
        _aiServiceMock.Verify(
            x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                reportHtml,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneScheduleFails_LogsErrorAndContinues()
    {
        // Arrange
        var currentDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        var currentHour = DateTime.UtcNow.Hour;

        var org = CreateTestOrganization();
        var user1 = CreateTestUser(org.Id, "user1@test.com", "User 1");
        var user2 = CreateTestUser(org.Id, "user2@test.com", "User 2");
        var schedule1 = CreateTestSchedule(user1.Id, org.Id, currentDayOfWeek, currentHour);
        var schedule2 = CreateTestSchedule(user2.Id, org.Id, currentDayOfWeek, currentHour);

        // Add focus score data for both users
        var focusScore1 = CreateTestFocusScore(user1.Id, org.Id);
        var focusScore2 = CreateTestFocusScore(user2.Id, org.Id);

        _context.Organizations.Add(org);
        _context.Users.AddRange(user1, user2);
        _context.WeeklyReportSchedules.AddRange(schedule1, schedule2);
        _context.DailyFocusScores.AddRange(focusScore1, focusScore2);
        await _context.SaveChangesAsync();

        var reportHtml = "<html>Relatorio Semanal</html>";
        _aiServiceMock
            .SetupSequence(x => x.GenerateWeeklyEmailReportAsync(
                It.IsAny<WeeklyEmailReportContext>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI service failed"))
            .ReturnsAsync(reportHtml);

        // Act
        await _job.ExecuteAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending weekly report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Second user should still get processed
        _emailServiceMock.Verify(
            x => x.SendWeeklyReportEmailAsync(
                user2.Email,
                user2.DisplayName,
                reportHtml,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper methods

    private static Organization CreateTestOrganization()
    {
        var org = Organization.Create("Test Org", "test-org", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        return org;
    }

    private static User CreateTestUser(Guid orgId, string email = "test@test.com", string displayName = "Test User")
    {
        return User.Create(orgId, email, "hash", displayName, UserRole.Colaborador);
    }

    private static WeeklyReportSchedule CreateTestSchedule(
        Guid userId,
        Guid orgId,
        int dayOfWeek,
        int? hour = null)
    {
        var currentHour = hour ?? DateTime.UtcNow.Hour;
        return WeeklyReportSchedule.Create(
            userId,
            orgId,
            dayOfWeek,
            new TimeOnly(currentHour, 0),
            "{}");
    }

    private static DailyFocusScore CreateTestFocusScore(
        Guid userId,
        Guid orgId,
        int productiveSeconds = 4000,
        int distractionSeconds = 600,
        int neutralSeconds = 1400,
        DateOnly? date = null)
    {
        // Create score for today to ensure it falls within the current week
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scoreDate = date ?? today;

        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var focusMs = productiveSeconds * 1000L;
        var distractionMs = distractionSeconds * 1000L;

        var score = DailyFocusScore.Create(
            Guid.NewGuid(),
            orgId,
            userId,
            scoreDate,
            totalTrackedMs: totalMs,
            focusTimeMs: focusMs,
            distractionMs: distractionMs,
            distractionCount: 2,
            pauseCount: 1,
            idleCount: 0,
            longFocusBlockCount: 1,
            focusScore: 75);

        // Also set the computed fields that the WeeklyReportDataCollector expects
        score.UpdateFeatures(
            productiveSeconds: productiveSeconds,
            distractionSeconds: distractionSeconds,
            neutralSeconds: neutralSeconds,
            contextSwitchesCount: 10,
            interruptionCount: 2,
            topAppExe: "VSCode.exe",
            topAppSeconds: productiveSeconds / 2,
            distinctAppsCount: 5,
            browserSeconds: 600,
            focusSessionsCount: 2,
            focusSessionsCompleted: 1,
            longestFocusSeconds: productiveSeconds / 3,
            avgFocusSeconds: productiveSeconds / 6);

        return score;
    }
}
