using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.InviteLinks.Commands;

public sealed record RevokeOrgInviteLinkCommand(
    Guid LinkId,
    Guid CallerOrgId) : IRequest<Unit>;

public sealed class RevokeOrgInviteLinkCommandHandler : IRequestHandler<RevokeOrgInviteLinkCommand, Unit>
{
    private readonly IInviteLinkRepository _inviteLinkRepository;

    public RevokeOrgInviteLinkCommandHandler(IInviteLinkRepository inviteLinkRepository)
    {
        _inviteLinkRepository = inviteLinkRepository;
    }

    public async Task<Unit> Handle(RevokeOrgInviteLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await _inviteLinkRepository.GetByIdAsync(request.LinkId, cancellationToken)
            ?? throw new NotFoundException("OrgInviteLink", request.LinkId);

        if (link.OrgId != request.CallerOrgId)
            throw new ForbiddenException("Invite link does not belong to your organization");

        link.Revoke();
        await _inviteLinkRepository.UpdateAsync(link, cancellationToken);

        return Unit.Value;
    }
}
