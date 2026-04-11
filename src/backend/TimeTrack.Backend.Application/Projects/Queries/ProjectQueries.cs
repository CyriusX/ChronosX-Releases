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
    int? Page = null,
    int? PageSize = null) : IRequest<ListProjectsResponse>;

public sealed class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, ListProjectsResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserContext _currentUser;

    public ListProjectsQueryHandler(
        IProjectRepository projectRepository,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
    }

    public async Task<ListProjectsResponse> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException("User not associated with an organization");

        IEnumerable<Domain.Entities.Project> projects;

        if (request.ActiveOnly == true)
        {
            projects = await _projectRepository.GetActiveByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken);
        }
        else
        {
            projects = await _projectRepository.GetByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken);
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
            Projects = projectList.Select(ProjectResponseMapper.Map).ToList(),
            TotalCount = totalCount
        };
    }
}

internal static class ProjectResponseMapper
{
    public static ProjectResponse Map(Domain.Entities.Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Color = p.Color,
        Status = p.Status.ToString(),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        SyncSource = p.SyncSource.ToString(),
        LinearProjectId = p.LinearProjectId,
        LastSyncedAt = p.LastSyncedAt
    };
}

/// <summary>
/// Query para obter um projeto específico
/// </summary>
public sealed record GetProjectQuery(Guid ProjectId) : IRequest<ProjectResponse>;

public sealed class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectQueryHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<ProjectResponse> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        return ProjectResponseMapper.Map(project);
    }
}
