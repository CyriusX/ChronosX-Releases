using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Auth;

public sealed class GetTeamStatusCommandTests
{
    [Fact]
    public async Task Handle_WithRecentHeartbeat_ShouldExposeTrackingState()
    {
        var orgId = Guid.NewGuid();
        var user = User.Create(orgId, "u1@example.com", "hash", "User 1", UserRole.Admin);

        var userRepo = new Mock<IUserRepository>();
        var sessionRepo = new Mock<IActivitySessionRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var currentUser = new Mock<ICurrentUserContext>();

        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        userRepo.Setup(r => r.GetByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        sessionRepo.Setup(r => r.GetByOrgIdAndDateRangeAsync(orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActivitySession>());

        var device = Device.Create(Guid.NewGuid(), orgId, user.Id, "host", "1.0.0", DisplayMode.Background);
        SetProperty(device, nameof(Device.LastHeartbeatAt), DateTime.UtcNow);
        SetProperty(device, nameof(Device.TrackingState), "stopped");

        deviceRepo.Setup(r => r.GetActiveByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device });

        var handler = new GetTeamStatusCommandHandler(
            userRepo.Object,
            sessionRepo.Object,
            deviceRepo.Object,
            currentUser.Object,
            NullLogger<GetTeamStatusCommandHandler>.Instance);

        var result = await handler.Handle(new GetTeamStatusCommand(orgId, "UTC"), CancellationToken.None);

        result.Members.Should().HaveCount(1);
        result.Members[0].TrackingState.Should().Be("stopped");
        result.Members[0].IsTracking.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithIdleHeartbeat_ShouldTreatAsTrackingForCompat()
    {
        var orgId = Guid.NewGuid();
        var user = User.Create(orgId, "u1@example.com", "hash", "User 1", UserRole.Admin);

        var userRepo = new Mock<IUserRepository>();
        var sessionRepo = new Mock<IActivitySessionRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var currentUser = new Mock<ICurrentUserContext>();

        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        userRepo.Setup(r => r.GetByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        sessionRepo.Setup(r => r.GetByOrgIdAndDateRangeAsync(orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActivitySession>());

        var device = Device.Create(Guid.NewGuid(), orgId, user.Id, "host", "1.0.0", DisplayMode.Background);
        SetProperty(device, nameof(Device.LastHeartbeatAt), DateTime.UtcNow);
        SetProperty(device, nameof(Device.TrackingState), "idle");

        deviceRepo.Setup(r => r.GetActiveByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device });

        var handler = new GetTeamStatusCommandHandler(
            userRepo.Object,
            sessionRepo.Object,
            deviceRepo.Object,
            currentUser.Object,
            NullLogger<GetTeamStatusCommandHandler>.Instance);

        var result = await handler.Handle(new GetTeamStatusCommand(orgId, "UTC"), CancellationToken.None);

        result.Members.Single().TrackingState.Should().Be("idle");
        result.Members.Single().IsTracking.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithStaleHeartbeat_ShouldMarkOffline()
    {
        var orgId = Guid.NewGuid();
        var user = User.Create(orgId, "u1@example.com", "hash", "User 1", UserRole.Admin);

        var userRepo = new Mock<IUserRepository>();
        var sessionRepo = new Mock<IActivitySessionRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var currentUser = new Mock<ICurrentUserContext>();

        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        userRepo.Setup(r => r.GetByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        sessionRepo.Setup(r => r.GetByOrgIdAndDateRangeAsync(orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActivitySession>());

        var device = Device.Create(Guid.NewGuid(), orgId, user.Id, "host", "1.0.0", DisplayMode.Background);
        SetProperty(device, nameof(Device.LastHeartbeatAt), DateTime.UtcNow.AddMinutes(-10));
        SetProperty(device, nameof(Device.TrackingState), "running");

        deviceRepo.Setup(r => r.GetActiveByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device });

        var handler = new GetTeamStatusCommandHandler(
            userRepo.Object,
            sessionRepo.Object,
            deviceRepo.Object,
            currentUser.Object,
            NullLogger<GetTeamStatusCommandHandler>.Instance);

        var result = await handler.Handle(new GetTeamStatusCommand(orgId, "UTC"), CancellationToken.None);

        result.Members.Single().TrackingState.Should().Be("offline");
        result.Members.Single().IsTracking.Should().BeFalse();
    }

    private static void SetProperty<T>(T instance, string propertyName, object? value)
    {
        var prop = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop.Should().NotBeNull($"Property {typeof(T).Name}.{propertyName} must exist");
        prop!.SetValue(instance, value);
    }
}

