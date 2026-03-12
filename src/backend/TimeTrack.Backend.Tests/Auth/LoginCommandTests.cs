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
/// Unit tests for LoginCommand
/// </summary>
public class LoginCommandTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _auditLogServiceMock = new Mock<IAuditLogService>();

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _auditLogServiceMock.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var user = CreateUser();
        var command = new LoginCommand("test@example.com", "password123");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString(), user.PasswordMustChange))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken("refresh-token"))
            .Returns("hashed-token");

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
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.UserId.Should().Be(user.Id);
        result.OrgId.Should().Be(user.OrgId);
        result.PasswordMustChange.Should().BeFalse();

        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_LogsAuditEntry()
    {
        // Arrange
        var user = CreateUser();
        var command = new LoginCommand("test@example.com", "password123");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString(), user.PasswordMustChange))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken("refresh-token"))
            .Returns("hashed-token");

        _tokenServiceMock
            .Setup(t => t.GetRefreshTokenExpiration())
            .Returns(TimeSpan.FromDays(90));

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenExpiration())
            .Returns(TimeSpan.FromMinutes(60));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify audit log was called
        _auditLogServiceMock.Verify(
            s => s.LogAsync(
                AuditActions.UserLogin,
                "user",
                user.Id,
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new LoginCommand("nonexistent@example.com", "password123");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ThrowsValidationException()
    {
        // Arrange
        var user = CreateUser();
        var command = new LoginCommand("test@example.com", "wrongpassword");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ThrowsForbiddenException()
    {
        // Arrange
        var user = CreateInactiveUser();
        var command = new LoginCommand("inactive@example.com", "password123");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithPasswordMustChange_ReturnsCorrectFlag()
    {
        // Arrange
        var user = CreateUserWithPasswordMustChange();
        var command = new LoginCommand("mustchange@example.com", "tempPassword");

        _userRepositoryMock
            .Setup(r => r.GetByEmailWithOrgAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString(), true))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashRefreshToken("refresh-token"))
            .Returns("hashed-token");

        _tokenServiceMock
            .Setup(t => t.GetRefreshTokenExpiration())
            .Returns(TimeSpan.FromDays(90));

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenExpiration())
            .Returns(TimeSpan.FromMinutes(60));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PasswordMustChange.Should().BeTrue();
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

    private static User CreateUserWithPasswordMustChange()
    {
        return User.Create(
            orgId: Guid.NewGuid(),
            email: "mustchange@example.com",
            passwordHash: "hashed-password",
            displayName: "Must Change User",
            role: UserRole.Colaborador,
            passwordMustChange: true
        );
    }
}
