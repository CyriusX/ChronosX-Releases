using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para login de usuário
/// </summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailWithOrgAsync(request.Email, cancellationToken);

        if (user == null)
        {
            throw new ValidationException("Credentials", "Invalid email or password");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Credentials", "Invalid email or password");
        }

        if (user.Status == UserStatus.Inactive)
        {
            throw new ForbiddenException("User account is deactivated");
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString());
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        // Create refresh token entity (device will be empty for web login)
        var refreshTokenEntity = Domain.Entities.RefreshToken.Create(
            user.Id,
            Guid.Empty, // No device for web login
            refreshTokenHash,
            _tokenService.GetRefreshTokenExpiration()
        );

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

        // Update last login
        user.RecordLogin();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = (int)_tokenService.GetAccessTokenExpiration().TotalSeconds,
            UserId = user.Id,
            OrgId = user.OrgId,
            DisplayName = user.DisplayName,
            OrgName = user.Organization?.Name ?? "",
            PasswordMustChange = user.PasswordMustChange
        };
    }
}
