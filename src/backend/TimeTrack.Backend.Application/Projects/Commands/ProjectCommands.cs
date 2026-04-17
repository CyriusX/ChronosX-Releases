using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Projects.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Projects.Commands;

/// <summary>
/// Command para criar um novo projeto
/// </summary>
public sealed record CreateProjectCommand(
    string Name,
    string? Description = null,
    string? Color = null,
    bool? IsBillable = null,
    string? Currency = null,
    decimal? HourlyRate = null) : IRequest<ProjectResponse>;

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _projectMembers;
    private readonly ICurrentUserContext _currentUser;

    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        IProjectMemberRepository projectMembers,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _projectMembers = projectMembers;
        _currentUser = currentUser;
    }

    public async Task<ProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException("User not associated with an organization");

        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var nameExists = await _projectRepository.NameExistsInOrgAsync(
            request.Name, _currentUser.OrgId.Value, null, cancellationToken);

        if (nameExists)
            throw new ValidationException("Name", "A project with this name already exists");

        var project = Domain.Entities.Project.Create(
            _currentUser.OrgId.Value,
            _currentUser.UserId.Value,
            request.Name,
            request.Description,
            request.Color,
            request.IsBillable ?? false,
            request.Currency,
            request.HourlyRate);

        await _projectRepository.AddAsync(project, cancellationToken);

        // Ensure the creator is a member so "My Data" shows newly created projects.
        var member = ProjectMember.Create(
            project.OrgId,
            project.Id,
            _currentUser.UserId.Value,
            _currentUser.UserId.Value,
            ProjectMemberRole.Owner);
        await _projectMembers.AddAsync(member, cancellationToken);

        return TimeTrack.Backend.Application.Projects.Queries.ProjectResponseMapper.Map(project, _currentUser);
    }
}

/// <summary>
/// Command para atualizar um projeto
/// </summary>
public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Name,
    string? Description = null,
    string? Color = null,
    bool? IsBillable = null,
    string? Currency = null,
    decimal? HourlyRate = null) : IRequest<ProjectResponse>;

public sealed class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateProjectCommandHandler(
        IProjectRepository projectRepository,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
    }

    public async Task<ProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        var nameExists = await _projectRepository.NameExistsInOrgAsync(
            request.Name, project.OrgId, request.ProjectId, cancellationToken);

        if (nameExists)
            throw new ValidationException("Name", "A project with this name already exists");

        project.Update(request.Name, request.Description, request.Color, request.IsBillable, request.Currency, request.HourlyRate);
        await _projectRepository.UpdateAsync(project, cancellationToken);

        return TimeTrack.Backend.Application.Projects.Queries.ProjectResponseMapper.Map(project, _currentUser);
    }
}

/// <summary>
/// Command para arquivar um projeto (soft delete)
/// </summary>
public sealed record ArchiveProjectCommand(Guid ProjectId) : IRequest<Unit>;

public sealed class ArchiveProjectCommandHandler : IRequestHandler<ArchiveProjectCommand, Unit>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserContext _currentUser;

    public ArchiveProjectCommandHandler(IProjectRepository projectRepository, ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        ProjectAuthorization.EnsureCanManageProject(project, _currentUser);

        project.Archive();
        await _projectRepository.UpdateAsync(project, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Command para reativar um projeto arquivado
/// </summary>
public sealed record ReactivateProjectCommand(Guid ProjectId) : IRequest<Unit>;

public sealed class ReactivateProjectCommandHandler : IRequestHandler<ReactivateProjectCommand, Unit>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserContext _currentUser;

    public ReactivateProjectCommandHandler(IProjectRepository projectRepository, ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ReactivateProjectCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        ProjectAuthorization.EnsureCanManageProject(project, _currentUser);

        project.Reactivate();
        await _projectRepository.UpdateAsync(project, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Command para excluir permanentemente um projeto
/// </summary>
public sealed record DeleteProjectCommand(Guid ProjectId) : IRequest<Unit>;

public sealed class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Unit>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _tasks;
    private readonly ICurrentUserContext _currentUser;

    public DeleteProjectCommandHandler(
        IProjectRepository projectRepository,
        IProjectTaskRepository tasks,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        ProjectAuthorization.EnsureCanManageProject(project, _currentUser);

        // Soft-delete project: hidden immediately; hard purge handled by a daily Hangfire job.
        project.SoftDelete(_currentUser.UserId.Value);
        await _projectRepository.UpdateAsync(project, cancellationToken);

        // Soft-delete tasks to keep boards/widgets/report breakdowns consistent (task query filter hides them).
        await _tasks.SoftDeleteByProjectAsync(project.Id, cancellationToken);

        return Unit.Value;
    }
}

internal static class ProjectAuthorization
{
    public static void EnsureCanManageProject(Project project, ICurrentUserContext currentUser)
    {
        if (currentUser.IsInRole(UserRole.Admin))
            return;

        if (!currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var isCreator = project.CreatedByUserId.HasValue && project.CreatedByUserId.Value == currentUser.UserId.Value;
        if (!isCreator)
            throw new ForbiddenException("You are not allowed to manage this project");
    }
}
