using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para atualizar role de um membro da organização
/// </summary>
public sealed record UpdateMemberRoleCommand(Guid OrgId, Guid UserId, string Role) : IRequest<Unit>;

public sealed class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand, Unit>
{
    private readonly IUserRepository _userRepository;

    public UpdateMemberRoleCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Unit> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        // Validate role
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException("Role", $"Invalid role. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}");
        }

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

        // Update role
        user.SetRole(role);

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Unit.Value;
    }
}
