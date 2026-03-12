using System.Reflection;
using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Auth;

/// <summary>
/// Unit tests for RefreshTokenCommand
/// </summary>
public class RefreshTokenCommandTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _tokenServiceMock = new Mock<ITokenService>();

        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _tokenServiceMock.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidRefreshToken_ReturnsNewTokens()
    {
        // Arrange
        var user = CreateUser();
        var storedToken = CreateRefreshToken(user);
        var command = new RefreshTokenCommand("valid-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken(command.RefreshToken))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashWithUserAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString(), false))
            .Returns("new-access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("new-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken("new-refresh-token"))
            .Returns("new-hashed-token");

        _tokenServiceMock
            .Setup(t => t.GetRefreshTokenExpiration())
            .Returns(TimeSpan.FromDays(90));

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenExpiration())
            .Returns(TimeSpan.FromMinutes(60));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
        result.ExpiresIn.Should().Be(3600);

        // Verify old token was revoked
        storedToken.IsRevoked.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.UpdateAsync(storedToken, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidRefreshToken_ThrowsValidationException()
    {
        // Arrange
        var command = new RefreshTokenCommand("invalid-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken(command.RefreshToken))
            .Returns("invalid-hash");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashWithUserAsync("invalid-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithRevokedRefreshToken_ThrowsValidationException()
    {
        // Arrange
        var user = CreateUser();
        var revokedToken = CreateRevokedRefreshToken(user);
        var command = new RefreshTokenCommand("revoked-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken(command.RefreshToken))
            .Returns("revoked-hash");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashWithUserAsync("revoked-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithExpiredRefreshToken_ThrowsValidationException()
    {
        // Arrange
        var user = CreateUser();
        var expiredToken = CreateExpiredRefreshToken(user);
        var command = new RefreshTokenCommand("expired-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken(command.RefreshToken))
            .Returns("expired-hash");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashWithUserAsync("expired-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithDeactivatedUser_ThrowsUserDeactivatedException()
    {
        // Arrange
        var inactiveUser = CreateInactiveUser();
        var storedToken = CreateRefreshToken(inactiveUser);
        var command = new RefreshTokenCommand("valid-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken(command.RefreshToken))
            .Returns("hashed-token");

        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashWithUserAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act & Assert
        await Assert.ThrowsAsync<UserDeactivatedException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    private static User CreateUser()
    {
        return User.Create(
            orgId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hashed-password",
            displayName: "Test User",
            role: UserRole.Colaborador
        );
    }

    private static User CreateInactiveUser()
    {
        var user = User.Create(
            orgId: Guid.NewGuid(),
            email: "inactive@example.com",
            passwordHash: "hashed-password",
            displayName: "Inactive User",
            role: UserRole.Colaborador
        );
        user.Deactivate();
        return user;
    }

    private static RefreshToken CreateRefreshToken(User user)
    {
        var token = RefreshToken.Create(
            userId: user.Id,
            deviceId: Guid.NewGuid(),
            tokenHash: "hashed-token",
            expiresIn: TimeSpan.FromDays(90)
        );

        // Set the User navigation property using reflection (private setter)
        var userProperty = typeof(RefreshToken).GetProperty("User",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        userProperty?.SetValue(token, user);

        return token;
    }

    private static RefreshToken CreateRevokedRefreshToken(User user)
    {
        var token = RefreshToken.Create(
            userId: user.Id,
            deviceId: Guid.NewGuid(),
            tokenHash: "revoked-hash",
            expiresIn: TimeSpan.FromDays(90)
        );
        token.Revoke();

        // Set the User navigation property using reflection (private setter)
        var userProperty = typeof(RefreshToken).GetProperty("User",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        userProperty?.SetValue(token, user);

        return token;
    }

    private static RefreshToken CreateExpiredRefreshToken(User user)
    {
        // Create token with already expired time
        var token = RefreshToken.Create(
            userId: user.Id,
            deviceId: Guid.NewGuid(),
            tokenHash: "expired-hash",
            expiresIn: TimeSpan.FromDays(-1) // Already expired
        );

        // Set the User navigation property using reflection (private setter)
        var userProperty = typeof(RefreshToken).GetProperty("User",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        userProperty?.SetValue(token, user);

        return token;
    }
}
