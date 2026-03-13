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
/// Unit tests for RetentionJob
/// </summary>
public class RetentionJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<IOrgPolicyRepository> _policyRepositoryMock;
    private readonly Mock<ILogger<RetentionJob>> _loggerMock;
    private readonly RetentionJob _job;

    public RetentionJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TimeTrackDbContext(options, null);
        _policyRepositoryMock = new Mock<IOrgPolicyRepository>();
        _loggerMock = new Mock<ILogger<RetentionJob>>();

        _job = new RetentionJob(_context, _policyRepositoryMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task RetentionJob_WhenNoOrganizations_LogsCompletion()
    {
        // Arrange - no organizations in database

        // Act
        await _job.ExecuteAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retention job completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RetentionJob_WithOrganization_DeletesOldDataBasedOnRetentionDays()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var retentionDays = 30;
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        // Create organization (Create takes 3 params: name, slug, orgType)
        var org = Organization.Create("Test Org", "test-org", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create user
        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);

        // Create device
        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);

        await _context.SaveChangesAsync();

        // Create old activity session (should be deleted)
        var oldSession = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "Old App",
            null,
            null,
            cutoffDate.AddDays(-1),
            cutoffDate,
            "old-key");

        // Create recent activity session (should NOT be deleted)
        var recentSession = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "Recent App",
            null,
            null,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            "recent-key");

        _context.ActivitySessions.AddRange(oldSession, recentSession);
        await _context.SaveChangesAsync();

        // Setup policy mock
        var policy = OrgPolicy.Create(org.Id);
        policy.GetType().GetProperty("RetentionDays")?.SetValue(policy, retentionDays);
        _policyRepositoryMock
            .Setup(x => x.GetByOrgIdAsync(org.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingSessions = await _context.ActivitySessions.IgnoreQueryFilters().CountAsync();
        remainingSessions.Should().Be(1); // Only recent session should remain
    }

    [Fact]
    public async Task RetentionJob_WithNoPolicy_UsesDefaultRetentionDays()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var defaultRetentionDays = 90; // Default
        var cutoffDate = DateTime.UtcNow.AddDays(-defaultRetentionDays);

        // Create organization
        var org = Organization.Create("Test Org 2", "test-org-2", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create user and device
        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);
        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        // Create old session (beyond default retention)
        var oldSession = ActivitySession.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            "Old App",
            null,
            null,
            cutoffDate.AddDays(-1),
            cutoffDate,
            "old-key");

        _context.ActivitySessions.Add(oldSession);
        await _context.SaveChangesAsync();

        // Setup policy mock to return null (no policy)
        _policyRepositoryMock
            .Setup(x => x.GetByOrgIdAsync(org.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrgPolicy?)null);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingSessions = await _context.ActivitySessions.IgnoreQueryFilters().CountAsync();
        remainingSessions.Should().Be(0); // Old session should be deleted
    }

    [Fact]
    public async Task RetentionJob_DeletesIdlePeriods()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var retentionDays = 30;
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        // Create organization
        var org = Organization.Create("Test Org 3", "test-org-3", OrgType.Business);
        org.GetType().GetProperty("Status")?.SetValue(org, OrgStatus.Active);
        _context.Organizations.Add(org);

        // Create user and device
        var user = User.Create(org.Id, "test@test.com", "hash", "Test User", UserRole.Colaborador);
        _context.Users.Add(user);
        var device = Device.Create(Guid.NewGuid(), org.Id, userId, "TEST-HOSTNAME", "1.0.0", DisplayMode.Background);
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        // Create old idle period (should be deleted)
        var oldIdlePeriod = IdlePeriod.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            cutoffDate.AddDays(-1),
            cutoffDate,
            "old-idle-key");

        // Create recent idle period (should NOT be deleted)
        var recentIdlePeriod = IdlePeriod.Create(
            Guid.NewGuid(),
            org.Id,
            device.Id,
            userId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            "recent-idle-key");

        _context.IdlePeriods.AddRange(oldIdlePeriod, recentIdlePeriod);
        await _context.SaveChangesAsync();

        // Setup policy mock
        var policy = OrgPolicy.Create(org.Id);
        policy.GetType().GetProperty("RetentionDays")?.SetValue(policy, retentionDays);
        _policyRepositoryMock
            .Setup(x => x.GetByOrgIdAsync(org.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingIdlePeriods = await _context.IdlePeriods.IgnoreQueryFilters().CountAsync();
        remainingIdlePeriods.Should().Be(1); // Only recent idle period should remain
    }
}
