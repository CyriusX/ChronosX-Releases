using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Projects.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Projects.Queries;

/// <summary>
/// Query para listar projetos da organização
/// </summary>
public sealed record ListProjectsQuery(
    bool? ActiveOnly = null,
    bool MineOnly = false,
    int? Page = null,
    int? PageSize = null) : IRequest<ListProjectsResponse>;

public sealed class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, ListProjectsResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _projectMembers;
    private readonly ICurrentUserContext _currentUser;

    public ListProjectsQueryHandler(
        IProjectRepository projectRepository,
        IProjectMemberRepository projectMembers,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _projectMembers = projectMembers;
        _currentUser = currentUser;
    }

    public async Task<ListProjectsResponse> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException("User not associated with an organization");

        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        IEnumerable<Domain.Entities.Project> projects;

        if (request.ActiveOnly == true)
        {
            projects = await _projectRepository.GetActiveByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken);
        }
        else
        {
            projects = await _projectRepository.GetByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken);
        }

        // Default behavior for non-managers: only return projects the user is a member of.
        // Managers/admins can explicitly request all org projects via mineOnly=false.
        var role = _currentUser.Role;
        var isManager = role == Domain.ValueObjects.UserRole.Admin || role == Domain.ValueObjects.UserRole.Gestor;
        var mineOnlyEffective = request.MineOnly || !isManager;

        if (mineOnlyEffective)
        {
            var myProjectIds = await _projectMembers.ListProjectIdsForUserAsync(_currentUser.UserId.Value, cancellationToken);
            var set = myProjectIds.Count > 0
                ? myProjectIds.ToHashSet()
                : new HashSet<Guid>();
            projects = projects.Where(p => set.Contains(p.Id));
        }

        var projectList = projects.ToList();
        var totalCount = projectList.Count;

        // Apply pagination if specified
        if (request.Page.HasValue && request.PageSize.HasValue && request.Page > 0 && request.PageSize > 0)
        {
            var skip = (request.Page.Value - 1) * request.PageSize.Value;
            projectList = projectList.Skip(skip).Take(request.PageSize.Value).ToList();
        }

        return new ListProjectsResponse
        {
            Projects = projectList.Select(p => ProjectResponseMapper.Map(p, _currentUser)).ToList(),
            TotalCount = totalCount
        };
    }
}

internal static class ProjectResponseMapper
{
    public static ProjectResponse Map(Domain.Entities.Project p, ICurrentUserContext currentUser)
    {
        var canManage = CanManage(p, currentUser);
        var status = p.Status.ToString();

        return new ProjectResponse
    {
        Id = p.Id,
        CreatedByUserId = p.CreatedByUserId,
        Name = p.Name,
        Description = p.Description,
        Color = p.Color,
        Status = status,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        CanArchive = canManage && status == nameof(Domain.ValueObjects.ProjectStatus.Active),
        CanReactivate = canManage && status == nameof(Domain.ValueObjects.ProjectStatus.Archived),
        CanDelete = canManage,
        IsBillable = p.IsBillable,
        Currency = p.Currency,
        HourlyRate = p.HourlyRate,
        SyncSource = p.SyncSource.ToString(),
        LinearProjectId = p.LinearProjectId,
        LastSyncedAt = p.LastSyncedAt
    };
    }

    private static bool CanManage(Domain.Entities.Project p, ICurrentUserContext currentUser)
    {
        if (currentUser.IsInRole(Domain.ValueObjects.UserRole.Admin))
            return true;

        if (!currentUser.UserId.HasValue)
            return false;

        return p.CreatedByUserId.HasValue && p.CreatedByUserId.Value == currentUser.UserId.Value;
    }
}

/// <summary>
/// Query para obter um projeto específico
/// </summary>
public sealed record GetProjectQuery(Guid ProjectId) : IRequest<ProjectResponse>;

public sealed class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _projectMembers;
    private readonly ICurrentUserContext _currentUser;

    public GetProjectQueryHandler(
        IProjectRepository projectRepository,
        IProjectMemberRepository projectMembers,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _projectMembers = projectMembers;
        _currentUser = currentUser;
    }

    public async Task<ProjectResponse> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        var role = _currentUser.Role;
        var isManager = role == Domain.ValueObjects.UserRole.Admin || role == Domain.ValueObjects.UserRole.Gestor;
        if (!isManager)
        {
            var isMember = await _projectMembers.IsMemberAsync(project.Id, _currentUser.UserId.Value, cancellationToken);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        return ProjectResponseMapper.Map(project, _currentUser);
    }
}
