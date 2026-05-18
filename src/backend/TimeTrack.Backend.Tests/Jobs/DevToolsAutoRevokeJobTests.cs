using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.Jobs;

public sealed class DevToolsAutoRevokeJobTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRevokeExpiredFlags_AndQueueDisableCommands()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(adminUserId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);

        var user = User.Create(orgId, "expired@example.com", "hash", "Expired", UserRole.Colaborador);
        user.SetDevToolsEnabledUntilUtc(DateTime.UtcNow.AddMinutes(-5));
        db.Users.Add(user);

        var device = Device.Create(Guid.NewGuid(), orgId, user.Id, "host", "1.0.0", DisplayMode.Background);
        db.Devices.Add(device);

        await db.SaveChangesAsync(CancellationToken.None);

        var job = new DevToolsAutoRevokeJob(db, NullLogger<DevToolsAutoRevokeJob>.Instance);
        await job.ExecuteAsync();

        var userAfter = await db.Users.FirstAsync(u => u.Id == user.Id, CancellationToken.None);
        userAfter.DevToolsEnabledUntilUtc.Should().BeNull();

        var cmds = await db.RemoteCommands.ToListAsync(CancellationToken.None);
        cmds.Should().HaveCount(1);
        cmds[0].CommandType.Should().Be("set_devtools");
        cmds[0].CreatedByUserId.Should().Be(Guid.Empty);
        cmds[0].PayloadJson.Should().NotBeNull();

        using var doc = JsonDocument.Parse(cmds[0].PayloadJson!);
        doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeFalse();
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
        public bool IsPlatformAdmin => false;
        public bool IsInRole(UserRole role) => Role == role;
    }
}

