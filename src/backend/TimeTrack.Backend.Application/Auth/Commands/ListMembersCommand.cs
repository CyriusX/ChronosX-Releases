using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para listar membros de uma organização
/// </summary>
public sealed record ListMembersCommand(Guid OrgId) : IRequest<ListMembersResponse>;

public sealed class ListMembersCommandHandler : IRequestHandler<ListMembersCommand, ListMembersResponse>
{
    private readonly Domain.Interfaces.Repositories.IUserRepository _userRepository;
    private readonly ICurrentUserContext _currentUser;

    public ListMembersCommandHandler(
        Domain.Interfaces.Repositories.IUserRepository userRepository,
        ICurrentUserContext currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<ListMembersResponse> Handle(ListMembersCommand request, CancellationToken cancellationToken)
    {
        // Verify user belongs to the org
        if (_currentUser.OrgId != request.OrgId)
        {
            throw new ForbiddenException("Access denied to this organization");
        }

        var users = await _userRepository.GetByOrgIdAsync(request.OrgId, cancellationToken);

        var members = users.Select(u => new MemberListItem
        {
            UserId = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            Role = u.Role.ToString(),
            Status = u.Status.ToString(),
            CreatedAt = u.CreatedAt
        }).ToList();

        return new ListMembersResponse
        {
            Members = members,
            TotalCount = members.Count
        };
    }
}
