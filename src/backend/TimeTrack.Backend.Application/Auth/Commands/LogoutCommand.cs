using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para logout (revogar refresh token)
/// </summary>
public sealed record LogoutCommand(string? RefreshToken) : IRequest<Unit>;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLogService;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ICurrentUserContext currentUser,
        IAuditLogService auditLogService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // If specific refresh token provided, revoke it
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
            var token = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (token != null)
            {
                token.Revoke();
                await _refreshTokenRepository.UpdateAsync(token, cancellationToken);
            }
        }
        // Otherwise, revoke all tokens for current user
        else if (_currentUser.UserId.HasValue)
        {
            await _refreshTokenRepository.RevokeAllByUserIdAsync(_currentUser.UserId.Value, cancellationToken);
        }

        // Audit log - user.logout (fire-and-forget)
        _auditLogService.LogAsync(
            AuditActions.UserLogout,
            "user",
            _currentUser.UserId,
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
