using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.ProjectMembers.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.ProjectMembers.Queries;

public sealed record ListProjectMembersQuery(Guid ProjectId) : IRequest<ListProjectMembersResponse>;

public sealed class ListProjectMembersQueryHandler : IRequestHandler<ListProjectMembersQuery, ListProjectMembersResponse>
{
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;
    private readonly ICurrentUserContext _currentUser;

    public ListProjectMembersQueryHandler(
        IProjectMemberRepository members,
        IProjectRepository projects,
        ICurrentUserContext currentUser)
    {
        _members = members;
        _projects = projects;
        _currentUser = currentUser;
    }

    public async Task<ListProjectMembersResponse> Handle(ListProjectMembersQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;
        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(project.Id, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        var members = await _members.ListByProjectAsync(project.Id, ct);

        var responses = members.Select(m => new ProjectMemberResponse
        {
            Id = m.Id,
            ProjectId = m.ProjectId,
            UserId = m.UserId,
            DisplayName = m.User?.DisplayName ?? string.Empty,
            Email = m.User?.Email ?? string.Empty,
            Role = m.Role.ToString(),
            AddedAt = m.AddedAt
        }).ToList();

        return new ListProjectMembersResponse { Members = responses, TotalCount = responses.Count };
    }
}
