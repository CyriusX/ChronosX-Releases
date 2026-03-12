using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Audit;

/// <summary>
/// Unit tests for Audit Log functionality
/// </summary>
public class AuditLogTests
{
    [Fact]
    public void AuditLog_Create_CreatesValidAuditLog()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var auditLog = AuditLog.Create(
            orgId: orgId,
            userId: userId,
            action: AuditActions.UserLogin,
            entityType: "user",
            entityId: entityId,
            oldValues: null,
            newValues: "{\"email\":\"test@example.com\"}",
            ipAddress: "127.0.0.1",
            userAgent: "Test Agent");

        // Assert
        auditLog.Should().NotBeNull();
        auditLog.OrgId.Should().Be(orgId);
        auditLog.UserId.Should().Be(userId);
        auditLog.Action.Should().Be(AuditActions.UserLogin);
        auditLog.EntityType.Should().Be("user");
        auditLog.EntityId.Should().Be(entityId);
        auditLog.IpAddress.Should().Be("127.0.0.1");
        auditLog.UserAgent.Should().Be("Test Agent");
        auditLog.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AuditLog_Create_RequiresAction()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Act & Assert
        var act = () => AuditLog.Create(
            orgId: orgId,
            userId: null,
            action: "",
            entityType: "user",
            entityId: null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("action");
    }

    [Fact]
    public void AuditLog_Create_RequiresEntityType()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Act & Assert
        var act = () => AuditLog.Create(
            orgId: orgId,
            userId: null,
            action: "user.login",
            entityType: "",
            entityId: null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("entityType");
    }

    [Fact]
    public void AuditLog_Create_WithNullUser_CreatesSystemAuditLog()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Act
        var auditLog = AuditLog.Create(
            orgId: orgId,
            userId: null,
            action: "system.startup",
            entityType: "system",
            entityId: null);

        // Assert
        auditLog.UserId.Should().BeNull();
    }
}

/// <summary>
/// Tests for AuditActions constants
/// </summary>
public class AuditActionsTests
{
    [Fact]
    public void AuditActions_HasCorrectValues()
    {
        // Assert
        AuditActions.UserLogin.Should().Be("user.login");
        AuditActions.UserLogout.Should().Be("user.logout");
        AuditActions.UserInviteAccepted.Should().Be("user.invite_accepted");
        AuditActions.UserRemoved.Should().Be("user.removed");
        AuditActions.UserReactivated.Should().Be("user.reactivated");
        AuditActions.PolicyUpdated.Should().Be("policy.updated");
        AuditActions.ReportAccessed.Should().Be("report.accessed");
        AuditActions.DeviceRegistered.Should().Be("device.registered");
        AuditActions.DeviceReactivated.Should().Be("device.reactivated");
        AuditActions.MemberRoleChanged.Should().Be("member.role_changed");
    }
}
