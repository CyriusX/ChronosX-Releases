using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.ProjectMembers.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.ProjectMembers.Queries;

public sealed record ListProjectMembersQuery(Guid ProjectId) : IRequest<ListProjectMembersResponse>;

public sealed class ListProjectMembersQueryHandler : IRequestHandler<ListProjectMembersQuery, ListProjectMembersResponse>
{
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;

    public ListProjectMembersQueryHandler(IProjectMemberRepository members, IProjectRepository projects)
    {
        _members = members;
        _projects = projects;
    }

    public async Task<ListProjectMembersResponse> Handle(ListProjectMembersQuery request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

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
