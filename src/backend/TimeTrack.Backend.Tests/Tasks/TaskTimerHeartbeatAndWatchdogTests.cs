using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Tasks;

public sealed class TaskTimerHeartbeatAndWatchdogTests
{
    [Fact]
    public async Task Heartbeat_ShouldPauseOpenTaskTimer_WhenTrackingStateIsNotRunning()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);

        var user = User.Create(
            orgId,
            "test@example.com",
            "password-hash",
            "Test User",
            UserRole.Admin);

        // Use reflection to set the Id to match the deviceId's userId
        var idProperty = typeof(User).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        db.Users.Add(user);

        var device = Device.Create(
            deviceId,
            orgId,
            userId,
            hostname: "test-host",
            agentVersion: "1.0.0",
            displayMode: DisplayMode.Background);
        db.Devices.Add(device);

        var open = TaskTimeEntry.Open(orgId, Guid.NewGuid(), userId);
        db.TaskTimeEntries.Add(open);
        await db.SaveChangesAsync(CancellationToken.None);

        var subscriptionServiceMock = new Mock<ISubscriptionService>();
        subscriptionServiceMock
            .Setup(s => s.CheckSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionCheckResult
            {
                HasAccess = true,
                Status = "active"
            });

        var handler = new HeartbeatCommandHandler(
            new DeviceRepository(db),
            new RemoteCommandRepository(db),
            subscriptionServiceMock.Object,
            new TaskTimeEntryRepository(db));

        await handler.Handle(new HeartbeatCommand(deviceId, null, "Windows", null, null, "paused", null, null, null, null, null), CancellationToken.None);

        var entryAfter = await db.TaskTimeEntries.FirstAsync(e => e.Id == open.Id, CancellationToken.None);
        entryAfter.IsPaused.Should().BeTrue();
        entryAfter.PausedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StaleWatchdog_ShouldPauseOpenTaskTimer_AtLastHeartbeat()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);

        var lastHeartbeatAt = DateTime.UtcNow.AddMinutes(-20);

        var device = Device.Create(
            deviceId,
            orgId,
            userId,
            hostname: "test-host",
            agentVersion: "1.0.0",
            displayMode: DisplayMode.Background);
        SetProperty(device, nameof(Device.LastHeartbeatAt), lastHeartbeatAt);
        db.Devices.Add(device);

        var open = TaskTimeEntry.Open(orgId, Guid.NewGuid(), userId);
        SetProperty(open, nameof(TaskTimeEntry.StartedAt), DateTime.UtcNow.AddMinutes(-30));
        db.TaskTimeEntries.Add(open);

        await db.SaveChangesAsync(CancellationToken.None);

        var job = new TaskTimerStalePauseJob(db, NullLogger<TaskTimerStalePauseJob>.Instance);
        await job.ExecuteAsync();

        var entryAfter = await db.TaskTimeEntries.FirstAsync(e => e.Id == open.Id, CancellationToken.None);
        entryAfter.IsPaused.Should().BeTrue();
        entryAfter.PausedAt.Should().BeCloseTo(lastHeartbeatAt, precision: TimeSpan.FromSeconds(1));
    }

    private static void SetProperty<T>(T instance, string propertyName, object? value)
    {
        var prop = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop.Should().NotBeNull($"Property {typeof(T).Name}.{propertyName} must exist");
        prop!.SetValue(instance, value);
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(Guid userId, Guid orgId, UserRole role)
        {
            UserId = userId;
            OrgId = orgId;
            Role = role;
        }

        public Guid? UserId { get; }
        public Guid? OrgId { get; }
        public Guid? DeviceId => null;
        public UserRole? Role { get; }
        public bool IsAuthenticated => false; // bypass multi-tenant filters in unit tests
        public bool IsPlatformAdmin => false;
        public bool IsInRole(UserRole role) => Role == role;
    }
}
