using MediatR;
using TimeTrack.Backend.Application.InviteLinks.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.InviteLinks.Queries;

public sealed record ListOrgInviteLinksQuery(Guid OrgId) : IRequest<ListInviteLinksResponse>;

public sealed class ListOrgInviteLinksQueryHandler : IRequestHandler<ListOrgInviteLinksQuery, ListInviteLinksResponse>
{
    private readonly IInviteLinkRepository _inviteLinkRepository;

    public ListOrgInviteLinksQueryHandler(IInviteLinkRepository inviteLinkRepository)
    {
        _inviteLinkRepository = inviteLinkRepository;
    }

    public async Task<ListInviteLinksResponse> Handle(ListOrgInviteLinksQuery request, CancellationToken cancellationToken)
    {
        var links = await _inviteLinkRepository.ListActiveByOrgIdAsync(request.OrgId, cancellationToken);

        return new ListInviteLinksResponse
        {
            Links = links.Select(l => new InviteLinkItem
            {
                Id = l.Id,
                Role = l.Role.ToString(),
                UseCount = l.UseCount,
                ExpiresAt = l.ExpiresAt,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt
            }).ToList()
        };
    }
}
