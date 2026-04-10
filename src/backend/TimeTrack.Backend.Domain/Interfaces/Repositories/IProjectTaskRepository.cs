using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectTask>> ListAssignedToUserAsync(Guid userId, bool includeDone = false, CancellationToken cancellationToken = default);
    Task<double> GetMaxPositionInColumnAsync(Guid projectId, ProjectTaskStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(ProjectTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProjectTask task, CancellationToken cancellationToken = default);
}
