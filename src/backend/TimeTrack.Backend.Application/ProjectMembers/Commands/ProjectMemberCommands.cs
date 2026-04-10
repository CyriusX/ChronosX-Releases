using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Notifications;
using TimeTrack.Backend.Application.ProjectMembers.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.ProjectMembers.Commands;

// ═══════════════════════════════════════════════════════════════════════════
// ADD MEMBER
// ═══════════════════════════════════════════════════════════════════════════

public sealed record AddProjectMemberCommand(Guid ProjectId, Guid UserId, string Role) : IRequest<ProjectMemberResponse>;

public sealed class AddProjectMemberCommandHandler : IRequestHandler<AddProjectMemberCommand, ProjectMemberResponse>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationDispatcher _notifications;

    public AddProjectMemberCommandHandler(
        IProjectRepository projects,
        IProjectMemberRepository members,
        IUserRepository users,
        ICurrentUserContext currentUser,
        INotificationDispatcher notifications)
    {
        _projects = projects;
        _members = members;
        _users = users;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<ProjectMemberResponse> Handle(AddProjectMemberCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        var role = _currentUser.Role;
        if (role != UserRole.Admin && role != UserRole.Gestor)
            throw new ForbiddenException("Only managers can manage project members");

        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        if (user.OrgId != project.OrgId)
            throw new ValidationException("UserId", "User must belong to the same organization");

        if (await _members.IsMemberAsync(project.Id, user.Id, ct))
            throw new ConflictException("member_exists", "User is already a member of this project");

        var memberRole = ParseRole(request.Role);
        var member = ProjectMember.Create(project.OrgId, project.Id, user.Id, _currentUser.UserId.Value, memberRole);
        await _members.AddAsync(member, ct);

        await _notifications.NotifyMembershipChangedAsync(user.Id, project, added: true, ct);

        return new ProjectMemberResponse
        {
            Id = member.Id,
            ProjectId = member.ProjectId,
            UserId = member.UserId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = member.Role.ToString(),
            AddedAt = member.AddedAt
        };
    }

    private static ProjectMemberRole ParseRole(string value) => value?.ToLowerInvariant() switch
    {
        "owner" => ProjectMemberRole.Owner,
        _ => ProjectMemberRole.Member
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// REMOVE MEMBER
// ═══════════════════════════════════════════════════════════════════════════

public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : IRequest<Unit>;

public sealed class RemoveProjectMemberCommandHandler : IRequestHandler<RemoveProjectMemberCommand, Unit>
{
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationDispatcher _notifications;

    public RemoveProjectMemberCommandHandler(
        IProjectMemberRepository members,
        IProjectRepository projects,
        ICurrentUserContext currentUser,
        INotificationDispatcher notifications)
    {
        _members = members;
        _projects = projects;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(RemoveProjectMemberCommand request, CancellationToken ct)
    {
        var role = _currentUser.Role;
        if (role != UserRole.Admin && role != UserRole.Gestor)
            throw new ForbiddenException("Only managers can manage project members");

        var member = await _members.GetAsync(request.ProjectId, request.UserId, ct)
            ?? throw new NotFoundException("ProjectMember", $"{request.ProjectId}/{request.UserId}");

        var project = await _projects.GetByIdAsync(request.ProjectId, ct);
        await _members.RemoveAsync(member, ct);

        if (project is not null)
            await _notifications.NotifyMembershipChangedAsync(request.UserId, project, added: false, ct);

        return Unit.Value;
    }
}
