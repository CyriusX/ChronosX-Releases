using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.InviteLinks.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.InviteLinks.Queries;

public sealed record GetOrgInviteLinkInfoQuery(string Token) : IRequest<InviteLinkInfoResponse>;

public sealed class GetOrgInviteLinkInfoQueryHandler : IRequestHandler<GetOrgInviteLinkInfoQuery, InviteLinkInfoResponse>
{
    private readonly IInviteLinkRepository _inviteLinkRepository;
    private readonly ITokenService _tokenService;

    public GetOrgInviteLinkInfoQueryHandler(
        IInviteLinkRepository inviteLinkRepository,
        ITokenService tokenService)
    {
        _inviteLinkRepository = inviteLinkRepository;
        _tokenService = tokenService;
    }

    public async Task<InviteLinkInfoResponse> Handle(GetOrgInviteLinkInfoQuery request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.Token);
        var link = await _inviteLinkRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException("OrgInviteLink", request.Token);

        return new InviteLinkInfoResponse
        {
            OrgName = link.Organization?.Name ?? "",
            Role = link.Role.ToString(),
            IsValid = link.IsValid
        };
    }
}
