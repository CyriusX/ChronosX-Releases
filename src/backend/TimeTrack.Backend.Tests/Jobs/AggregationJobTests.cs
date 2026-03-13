using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.Jobs;

/// <summary>
/// Unit tests for AggregationJob
/// </summary>
public class AggregationJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<IDailySummaryRepository> _summaryRepositoryMock;
    private readonly Mock<ILogger<AggregationJob>> _loggerMock;
    private readonly AggregationJob _job;

    public AggregationJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TimeTrackDbContext(options, null);
        _summaryRepositoryMock = new Mock<IDailySummaryRepository>();
        _loggerMock = new Mock<ILogger<AggregationJob>>();

        _job = new AggregationJob(_context, _summaryRepositoryMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AggregationJob_WhenNoActivitySessions_LogsCompletion()
    {
        // Arrange - no activity sessions in database

        // Act
        await _job.ExecuteForRecentDaysAsync(1);

        // Assert - verify completion message is logged once at the end
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Completed aggregation for")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AggregationJob_WithActivitySessions_CreatesDailySummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Create organization (Create takes 3 params: name, slug, orgType)
        var org = Organization.Create("Test Org", "test-org", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create user
        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);

        // Create device (Create takes 7 params: id, orgId, userId, hostname, agentVersion, displayMode, deviceName?)
        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);

        await _context.SaveChangesAsync();

        // Create activity sessions for today
        var session1 = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "App 1",
            null,
            null,
            today.AddHours(8),
            today.AddHours(9),
            "key-1");

        var session2 = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "App 2",
            null,
            null,
            today.AddHours(10),
            today.AddHours(11),
            "key-2");

        _context.ActivitySessions.AddRange(session1, session2);
        await _context.SaveChangesAsync();

        // Setup repository mock
        var expectedSummary = DailySummary.Create(
            Guid.NewGuid(),
            org.Id,
            userId,
            today,
            7200, // 2 hours in seconds
            0,
            2);

        _summaryRepositoryMock
            .Setup(x => x.UpsertAsync(
                org.Id,
                userId,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        await _job.ExecuteForRecentDaysAsync(1);

        // Assert
        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                org.Id,
                userId,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AggregationJob_WithIdlePeriods_IncludesIdleTimeInSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Create organization
        var org = Organization.Create("Test Org 2", "test-org-2", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create user
        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);

        // Create device
        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);

        await _context.SaveChangesAsync();

        // Create activity session
        var session = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "App",
            null,
            null,
            today.AddHours(8),
            today.AddHours(9),
            "key-1");

        _context.ActivitySessions.Add(session);

        // Create idle period
        var idlePeriod = IdlePeriod.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            today.AddHours(9),
            today.AddHours(9).AddMinutes(30),
            "idle-key");

        _context.IdlePeriods.Add(idlePeriod);
        await _context.SaveChangesAsync();

        var expectedSummary = DailySummary.Create(
            Guid.NewGuid(),
            org.Id,
            userId,
            today,
            3600, // 1 hour active
            1800, // 30 min idle
            1);

        _summaryRepositoryMock
            .Setup(x => x.UpsertAsync(
                org.Id,
                userId,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        await _job.ExecuteForRecentDaysAsync(1);

        // Assert - Verify Upsert was called
        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                org.Id,
                userId,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AggregationJob_ProcessesMultipleDays()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Create organization
        var org = Organization.Create("Test Org 3", "test-org-3", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);

        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);

        await _context.SaveChangesAsync();

        // Create session for yesterday
        var yesterdaySession = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "App Yesterday",
            null,
            null,
            yesterday.AddHours(8),
            yesterday.AddHours(9),
            "key-yesterday");

        // Create session for today
        var todaySession = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "App Today",
            null,
            null,
            today.AddHours(8),
            today.AddHours(9),
            "key-today");

        _context.ActivitySessions.AddRange(yesterdaySession, todaySession);
        await _context.SaveChangesAsync();

        _summaryRepositoryMock
            .Setup(x => x.UpsertAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid orgId, Guid userId, DateTime date, int active, int idle, int count, CancellationToken ct) =>
                DailySummary.Create(Guid.NewGuid(), orgId, userId, date, active, idle, count));

        // Act
        await _job.ExecuteForRecentDaysAsync(2);

        // Assert - Should be called at least twice (once for each day with data)
        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                org.Id,
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task AggregationJob_WithMultipleUsers_CreatesSummaryPerUser()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Create organization
        var org = Organization.Create("Test Org 4", "test-org-4", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create users
        var user1 = User.Create(org.Id, "user1@test.com", "hash", "User 1", UserRole.Colaborador);
        var user2 = User.Create(org.Id, "user2@test.com", "hash", "User 2", UserRole.Colaborador);
        _context.Users.AddRange(user1, user2);

        // Create devices
        var device1 = Device.Create(Guid.NewGuid(), org.Id, userId1, "HOSTNAME-1", "1.0.0", DisplayMode.Background);
        var device2 = Device.Create(Guid.NewGuid(), org.Id, userId2, "HOSTNAME-2", "1.0.0", DisplayMode.Background);
        _context.Devices.AddRange(device1, device2);

        await _context.SaveChangesAsync();

        // Create sessions for both users
        var session1 = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device1.Id,
            userId1,
            "App User 1",
            null,
            null,
            today.AddHours(8),
            today.AddHours(9),
            "key-user1");

        var session2 = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device2.Id,
            userId2,
            "App User 2",
            null,
            null,
            today.AddHours(8),
            today.AddHours(9),
            "key-user2");

        _context.ActivitySessions.AddRange(session1, session2);
        await _context.SaveChangesAsync();

        _summaryRepositoryMock
            .Setup(x => x.UpsertAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid orgId, Guid userId, DateTime date, int active, int idle, int count, CancellationToken ct) =>
                DailySummary.Create(Guid.NewGuid(), orgId, userId, date, active, idle, count));

        // Act
        await _job.ExecuteForRecentDaysAsync(1);

        // Assert - Should be called for each user
        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                org.Id,
                userId1,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                org.Id,
                userId2,
                today,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
