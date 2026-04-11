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

    /// <summary>
    /// Returns every non-deleted, non-Done task whose DueDate falls on the given UTC calendar day
    /// AND has an assignee. Used by DeadlineScanJob to send "due today" notifications.
    /// </summary>
    Task<IReadOnlyList<ProjectTask>> ListAssignedTasksDueOnAsync(DateTime utcDate, CancellationToken cancellationToken = default);

    /// <summary>Find a Linear-sourced task by its external issue id.</summary>
    Task<ProjectTask?> GetByLinearIssueIdAsync(Guid orgId, string linearIssueId, CancellationToken cancellationToken = default);

    /// <summary>List all Linear-sourced tasks for a given project (for orphan cleanup on sync).</summary>
    Task<IReadOnlyList<ProjectTask>> ListLinearTasksForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
