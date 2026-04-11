using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Projects.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Projects.Commands;

/// <summary>
/// Command para criar um novo projeto
/// </summary>
public sealed record CreateProjectCommand(
    string Name,
    string? Description = null,
    string? Color = null) : IRequest<ProjectResponse>;

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        ICurrentUserContext currentUser)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
    }

    public async Task<ProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException("User not associated with an organization");

        var nameExists = await _projectRepository.NameExistsInOrgAsync(
            request.Name, _currentUser.OrgId.Value, null, cancellationToken);

        if (nameExists)
            throw new ValidationException("Name", "A project with this name already exists");

        var project = Domain.Entities.Project.Create(
            _currentUser.OrgId.Value,
            request.Name,
            request.Description,
            request.Color);

        await _projectRepository.AddAsync(project, cancellationToken);

        return TimeTrack.Backend.Application.Projects.Queries.ProjectResponseMapper.Map(project);
    }
}

/// <summary>
/// Command para atualizar um projeto
/// </summary>
public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Name,
    string? Description = null,
    string? Color = null) : IRequest<ProjectResponse>;

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

        project.Update(request.Name, request.Description, request.Color);
        await _projectRepository.UpdateAsync(project, cancellationToken);

        return TimeTrack.Backend.Application.Projects.Queries.ProjectResponseMapper.Map(project);
    }
}

/// <summary>
/// Command para arquivar um projeto (soft delete)
/// </summary>
public sealed record ArchiveProjectCommand(Guid ProjectId) : IRequest<Unit>;

public sealed class ArchiveProjectCommandHandler : IRequestHandler<ArchiveProjectCommand, Unit>
{
    private readonly IProjectRepository _projectRepository;

    public ArchiveProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Unit> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

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

    public ReactivateProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Unit> Handle(ReactivateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

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

    public DeleteProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project == null)
            throw new NotFoundException("Project", request.ProjectId);

        await _projectRepository.DeleteAsync(project, cancellationToken);

        return Unit.Value;
    }
}
