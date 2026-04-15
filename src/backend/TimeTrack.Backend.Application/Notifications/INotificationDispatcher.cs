using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Application.Notifications;

/// <summary>
/// Application-side service that creates persistent inbox entries
/// AND queues remote-command pushes to the agent (so the toast fires
/// on the next sync cycle).
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyTaskAssignedAsync(Guid userId, ProjectTask task, Project project, CancellationToken cancellationToken = default);
    Task NotifyTaskUnassignedAsync(Guid userId, ProjectTask task, Project project, CancellationToken cancellationToken = default);
    Task NotifyMembershipChangedAsync(Guid userId, Project project, bool added, CancellationToken cancellationToken = default);
    Task NotifyDeadlineTodayAsync(Guid userId, ProjectTask task, Project project, CancellationToken cancellationToken = default);
}
