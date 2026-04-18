using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.Commands;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Maintenance;

public sealed class DevToolsAccessTests
{
    [Fact]
    public async Task SetDeviceDevToolsAccess_ShouldSetUserFlag_AndQueueCommandsForAllActiveDevices()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(adminUserId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);

        var targetUser = User.Create(orgId, "devtools@example.com", "hash", "Target", UserRole.Colaborador);
        db.Users.Add(targetUser);

        var deviceA = Device.Create(Guid.NewGuid(), orgId, targetUser.Id, "host-a", "1.0.0", DisplayMode.Background);
        var deviceB = Device.Create(Guid.NewGuid(), orgId, targetUser.Id, "host-b", "1.0.0", DisplayMode.Background);
        db.Devices.AddRange(deviceA, deviceB);

        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new SetDeviceDevToolsAccessCommandHandler(
            new DeviceRepository(db),
            new UserRepository(db),
            new RemoteCommandRepository(db),
            ctx);

        var before = DateTime.UtcNow;
        var result = await handler.Handle(
            new SetDeviceDevToolsAccessCommand(orgId, deviceA.Id, Enabled: true),
            CancellationToken.None);

        result.UserId.Should().Be(targetUser.Id);
        result.Enabled.Should().BeTrue();
        result.ExpiresAtUtc.Should().NotBeNull();
        result.QueuedDeviceCount.Should().Be(2);

        var userAfter = await db.Users.FirstAsync(u => u.Id == targetUser.Id, CancellationToken.None);
        userAfter.DevToolsEnabledUntilUtc.Should().NotBeNull();
        userAfter.DevToolsEnabledUntilUtc!.Value.Should().BeAfter(before.AddHours(23.5));
        userAfter.DevToolsEnabledUntilUtc.Value.Should().BeBefore(before.AddHours(24.5));

        var commands = await db.RemoteCommands.ToListAsync(CancellationToken.None);
        commands.Should().HaveCount(2);
        commands.All(c => c.CommandType == "set_devtools").Should().BeTrue();

        foreach (var cmd in commands)
        {
            cmd.CreatedByUserId.Should().Be(adminUserId);
            cmd.PayloadJson.Should().NotBeNull();
            using var doc = JsonDocument.Parse(cmd.PayloadJson!);
            doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
            doc.RootElement.TryGetProperty("expiresAtUtc", out _).Should().BeTrue();
        }
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
        public bool IsAuthenticated => false;
        public bool IsInRole(UserRole role) => Role == role;
    }
}

