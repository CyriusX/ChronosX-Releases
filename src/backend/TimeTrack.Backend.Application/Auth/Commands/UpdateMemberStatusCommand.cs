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
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLogService;

    public UpdateMemberStatusCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ICurrentUserContext currentUser,
        IAuditLogService auditLogService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
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
        if (_currentUser.UserId == request.UserId)
        {
            throw new ValidationException("Status", "Cannot change your own status");
        }

        var status = request.Status.ToLowerInvariant();
        string? auditAction = null;

        if (status == "active")
        {
            user.Reactivate();
            auditAction = AuditActions.UserReactivated;
        }
        else if (status == "inactive")
        {
            user.Deactivate();
            // Revoke all refresh tokens to prevent continued Agent operation
            await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, cancellationToken);
            auditAction = AuditActions.UserRemoved;
        }
        else
        {
            throw new ValidationException("Status", "Invalid status. Use 'active' or 'inactive'");
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        // Audit log
        _auditLogService.LogAsync(
            auditAction,
            "user",
            user.Id,
            new { email = user.Email, displayName = user.DisplayName, newStatus = status },
            cancellationToken);

        return Unit.Value;
    }
}
