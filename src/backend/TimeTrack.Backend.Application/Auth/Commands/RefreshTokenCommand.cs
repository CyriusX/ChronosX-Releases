using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para refresh do token de acesso
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResponse>;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ISubscriptionService _subscriptionService;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ISubscriptionService subscriptionService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _subscriptionService = subscriptionService;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var storedToken = await _refreshTokenRepository.GetByTokenHashWithUserAsync(tokenHash, cancellationToken);

        if (storedToken == null)
        {
            throw new ValidationException("RefreshToken", "Invalid refresh token");
        }

        if (!storedToken.IsValid)
        {
            throw new ValidationException("RefreshToken", "Refresh token is expired or revoked");
        }

        var user = storedToken.User;

        if (user == null || user.Status == UserStatus.Inactive)
        {
            throw new UserDeactivatedException();
        }

        // Revoke old token (rotation)
        storedToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

        // Generate new tokens (include device_id from stored token if available)
        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.OrgId, storedToken.DeviceId, user.Role.ToString(), user.PasswordMustChange);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);

        // Create new refresh token
        var newTokenEntity = Domain.Entities.RefreshToken.Create(
            user.Id,
            storedToken.DeviceId,
            newRefreshTokenHash,
            _tokenService.GetRefreshTokenExpiration()
        );

        await _refreshTokenRepository.AddAsync(newTokenEntity, cancellationToken);

        var subscriptionCheck = await _subscriptionService.CheckSubscriptionAccessAsync(user.OrgId, cancellationToken);

        return new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = (int)_tokenService.GetAccessTokenExpiration().TotalSeconds,
            SubscriptionStatus = subscriptionCheck.Status,
            PlanTier = subscriptionCheck.PlanTier ?? ""
        };
    }
}
