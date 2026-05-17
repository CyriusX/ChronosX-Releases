using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.Queries;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Maintenance;

public sealed class ListAllOrganizationsQueryTests
{
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotPlatformAdmin()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Admin, IsPlatformAdmin: false);

        await using var db = new TimeTrackDbContext(options, ctx);

        var handler = new ListAllOrganizationsQueryHandler(
            new OrganizationRepository(db),
            new UserRepository(db),
            new DeviceRepository(db),
            ctx);

        var act = async () => await handler.Handle(new ListAllOrganizationsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only platform admins can list all organizations");
    }

    [Fact]
    public async Task Handle_ShouldReturnAllOrganizationsWithUserAndDeviceCounts_WhenUserIsPlatformAdmin()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(adminUserId, org1Id, UserRole.Admin, IsPlatformAdmin: true);

        await using var db = new TimeTrackDbContext(options, ctx);

        // Create two organizations
        var org1 = Organization.Create("Org One", "org-one", OrgType.Business);
        org1.GetType().GetProperty("Id")?.SetValue(org1, org1Id);
        org1.GetType().GetProperty("Status")?.SetValue(org1, OrgStatus.Active);

        var org2 = Organization.Create("Org Two", "org-two", OrgType.Personal);
        org2.GetType().GetProperty("Id")?.SetValue(org2, org2Id);
        org2.GetType().GetProperty("Status")?.SetValue(org2, OrgStatus.Active);

        db.Organizations.AddRange(org1, org2);

        // Create users for org1
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user1 = User.Create(org1Id, "user1@example.com", "hash", "User 1", UserRole.Admin);
        var user2 = User.Create(org1Id, "user2@example.com", "hash", "User 2", UserRole.Colaborador);
        user1.GetType().GetProperty("Id")?.SetValue(user1, user1Id);
        user2.GetType().GetProperty("Id")?.SetValue(user2, user2Id);

        // Create users for org2
        var user3Id = Guid.NewGuid();
        var user3 = User.Create(org2Id, "user3@example.com", "hash", "User 3", UserRole.Gestor);
        user3.GetType().GetProperty("Id")?.SetValue(user3, user3Id);

        db.Users.AddRange(user1, user2, user3);

        // Create devices for org1
        var device1 = Device.Create(Guid.NewGuid(), org1Id, user1.Id, "host-1", "1.0.0", DisplayMode.Background);
        var device2 = Device.Create(Guid.NewGuid(), org1Id, user2.Id, "host-2", "1.0.0", DisplayMode.Background);
        var inactiveDevice = Device.Create(Guid.NewGuid(), org1Id, user1.Id, "host-inactive", "1.0.0", DisplayMode.Background);
        inactiveDevice.Deactivate();

        // Create devices for org2
        var device3 = Device.Create(Guid.NewGuid(), org2Id, user3.Id, "host-3", "1.0.0", DisplayMode.Background);

        db.Devices.AddRange(device1, device2, inactiveDevice, device3);

        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new ListAllOrganizationsQueryHandler(
            new OrganizationRepository(db),
            new UserRepository(db),
            new DeviceRepository(db),
            ctx);

        var result = await handler.Handle(new ListAllOrganizationsQuery(), CancellationToken.None);

        result.Organizations.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);

        var org1Result = result.Organizations.FirstOrDefault(o => o.Id == org1Id);
        org1Result.Should().NotBeNull();
        org1Result!.Name.Should().Be("Org One");
        org1Result.Slug.Should().Be("org-one");
        org1Result.OrgType.Should().Be("Business");
        org1Result.Status.Should().Be("Active");
        org1Result.UserCount.Should().Be(2);
        org1Result.DeviceCount.Should().Be(2); // Only active devices

        var org2Result = result.Organizations.FirstOrDefault(o => o.Id == org2Id);
        org2Result.Should().NotBeNull();
        org2Result!.Name.Should().Be("Org Two");
        org2Result.Slug.Should().Be("org-two");
        org2Result.OrgType.Should().Be("Personal");
        org2Result.Status.Should().Be("Active");
        org2Result.UserCount.Should().Be(1);
        org2Result.DeviceCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnOrganizationsOrderedByName()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(adminUserId, orgId, UserRole.Admin, IsPlatformAdmin: true);

        await using var db = new TimeTrackDbContext(options, ctx);

        var org1 = Organization.Create("Zebra Corp", "zebra", OrgType.Business);
        var org2 = Organization.Create("Alpha Inc", "alpha", OrgType.Personal);
        var org3 = Organization.Create("Mid LLC", "mid", OrgType.Business);

        db.Organizations.AddRange(org1, org2, org3);

        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new ListAllOrganizationsQueryHandler(
            new OrganizationRepository(db),
            new UserRepository(db),
            new DeviceRepository(db),
            ctx);

        var result = await handler.Handle(new ListAllOrganizationsQuery(), CancellationToken.None);

        result.Organizations.Should().HaveCount(3);
        result.Organizations[0].Name.Should().Be("Alpha Inc");
        result.Organizations[1].Name.Should().Be("Mid LLC");
        result.Organizations[2].Name.Should().Be("Zebra Corp");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoOrganizationsExist()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(adminUserId, orgId, UserRole.Admin, IsPlatformAdmin: true);

        await using var db = new TimeTrackDbContext(options, ctx);

        var handler = new ListAllOrganizationsQueryHandler(
            new OrganizationRepository(db),
            new UserRepository(db),
            new DeviceRepository(db),
            ctx);

        var result = await handler.Handle(new ListAllOrganizationsQuery(), CancellationToken.None);

        result.Organizations.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(Guid userId, Guid orgId, UserRole role, bool IsPlatformAdmin)
        {
            UserId = userId;
            OrgId = orgId;
            Role = role;
            this.IsPlatformAdmin = IsPlatformAdmin;
        }

        public Guid? UserId { get; }
        public Guid? OrgId { get; }
        public Guid? DeviceId => null;
        public UserRole? Role { get; }
        public bool IsAuthenticated => false;
        public bool IsPlatformAdmin { get; }
        public bool IsInRole(UserRole role) => Role == role;
    }
}
