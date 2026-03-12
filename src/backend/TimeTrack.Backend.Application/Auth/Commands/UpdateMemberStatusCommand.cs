using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para atualizar status de um membro da organização
/// </summary>
public sealed record UpdateMemberStatusCommand(Guid OrgId, Guid UserId, string Status) : IRequest<Unit>;

public sealed class UpdateMemberStatusCommandHandler : IRequestHandler<UpdateMemberStatusCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public UpdateMemberStatusCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Unit> Handle(UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        // Verify user belongs to the organization
        if (user.OrgId != request.OrgId)
        {
            throw new ForbiddenException("User does not belong to this organization");
        }

        // Cannot deactivate yourself
        var currentUserContext = await GetCurrentUserContext();
        if (currentUserContext != null && currentUserContext.UserId == request.UserId)
        {
            throw new ValidationException("Status", "Cannot change your own status");
        }

        var status = request.Status.ToLowerInvariant();
        if (status == "active")
        {
            user.Reactivate();
        }
        else if (status == "inactive")
        {
            user.Deactivate();
            // Revoke all refresh tokens to prevent continued Agent operation
            await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, cancellationToken);
        }
        else
        {
            throw new ValidationException("Status", "Invalid status. Use 'active' or 'inactive'");
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Unit.Value;
    }

    // This will be injected via ICurrentUserContext in production
    private Task<CurrentUserContext?> GetCurrentUserContext() => Task.FromResult<CurrentUserContext?>(null);
}

/// <summary>
/// Simplified current user context for validation
/// </summary>
public sealed record CurrentUserContext(Guid UserId, Guid OrgId);
