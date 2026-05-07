using MediatR;
using Microsoft.Extensions.Configuration;
using TimeTrack.Backend.Application.InviteLinks.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.InviteLinks.Commands;

public sealed record GenerateOrgInviteLinkCommand(
    Guid OrgId,
    Guid CreatedByUserId) : IRequest<GenerateInviteLinkResponse>;

public sealed class GenerateOrgInviteLinkCommandHandler : IRequestHandler<GenerateOrgInviteLinkCommand, GenerateInviteLinkResponse>
{
    private readonly IInviteLinkRepository _inviteLinkRepository;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public GenerateOrgInviteLinkCommandHandler(
        IInviteLinkRepository inviteLinkRepository,
        ITokenService tokenService,
        IConfiguration configuration)
    {
        _inviteLinkRepository = inviteLinkRepository;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<GenerateInviteLinkResponse> Handle(GenerateOrgInviteLinkCommand request, CancellationToken cancellationToken)
    {
        var rawToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawToken);

        var link = OrgInviteLink.Create(
            request.OrgId,
            tokenHash,
            UserRole.Colaborador,
            request.CreatedByUserId);

        await _inviteLinkRepository.AddAsync(link, cancellationToken);

        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "https://app.timetrack.com";
        var linkUrl = $"{frontendBaseUrl}/join/{rawToken}";

        return new GenerateInviteLinkResponse
        {
            Id = link.Id,
            Token = rawToken,
            LinkUrl = linkUrl,
            Role = link.Role.ToString(),
            CreatedAt = link.CreatedAt
        };
    }
}
