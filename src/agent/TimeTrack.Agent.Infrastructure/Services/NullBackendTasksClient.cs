using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// No-op implementation for local/test mode (no backend).
/// </summary>
public sealed class NullBackendTasksClient : IBackendTasksClient
{
    public Task<TasksListResult?> ListMyTasksAsync(bool includeDone, CancellationToken ct = default)
        => Task.FromResult<TasksListResult?>(null);

    public Task<TaskDetail?> MoveTaskAsync(Guid taskId, string status, uint? rowVersion, CancellationToken ct = default)
        => Task.FromResult<TaskDetail?>(null);

    public Task<OpenTaskResult?> GetMyOpenTaskAsync(CancellationToken ct = default)
        => Task.FromResult<OpenTaskResult?>(null);

    public Task<bool> PauseMyOpenTaskAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<bool> ResumeMyOpenTaskAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<bool> CloseOpenTaskOnIdleRejectAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<NotificationsListResult?> ListMyNotificationsAsync(bool unreadOnly, int take, CancellationToken ct = default)
        => Task.FromResult<NotificationsListResult?>(null);

    public Task<bool> MarkNotificationReadAsync(Guid notificationId, CancellationToken ct = default) => Task.FromResult(false);

    public Task<ProjectListResult?> ListProjectsAsync(bool activeOnly, CancellationToken ct = default)
        => Task.FromResult<ProjectListResult?>(null);
}
