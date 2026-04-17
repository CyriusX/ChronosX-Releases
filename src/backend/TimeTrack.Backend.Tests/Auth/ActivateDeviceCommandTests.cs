using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Auth;

public sealed class ActivateDeviceCommandTests
{
    [Fact]
    public async Task Handle_WithExistingDeviceIdInOtherOrg_ThrowsConflictException()
    {
        // Arrange
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        var user = User.Create(
            orgB,
            "junior@example.com",
            "hash",
            "Junior",
            UserRole.Admin);

        var deviceId = Guid.NewGuid();
        var existingDeviceInOtherOrg = Device.Create(
            deviceId,
            orgA,
            Guid.NewGuid(),
            "OtherHost",
            "1.0.0",
            DisplayMode.Background,
            "OtherHost");

        var deviceRepository = new Mock<IDeviceRepository>();
        var userRepository = new Mock<IUserRepository>();
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var tokenService = new Mock<ITokenService>();
        var currentUser = new Mock<ICurrentUserContext>();
        var auditLog = new Mock<IAuditLogService>();

        currentUser.SetupGet(c => c.UserId).Returns(user.Id);
        currentUser.SetupGet(c => c.OrgId).Returns(orgB);

        userRepository
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        deviceRepository
            .Setup(r => r.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        deviceRepository
            .Setup(r => r.GetByIdUnfilteredAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDeviceInOtherOrg);

        var handler = new ActivateDeviceCommandHandler(
            deviceRepository.Object,
            userRepository.Object,
            refreshTokenRepository.Object,
            tokenService.Object,
            currentUser.Object,
            auditLog.Object);

        var command = new ActivateDeviceCommand(
            deviceId,
            "Juniors-MacBook-Air",
            "Juniors-MacBook-Air",
            "1.0.0",
            "background");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(act);
        ex.Code.Should().Be("device_id_conflict");

        deviceRepository.Verify(r => r.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        deviceRepository.Verify(r => r.UpdateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

