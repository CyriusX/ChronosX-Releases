namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Client for kanban task endpoints on the backend.
/// Used by agent IPC handlers to proxy desktop UI actions
/// (start/stop/complete a task, fetch my tasks, idle pause/resume).
/// </summary>
public interface IBackendTasksClient
{
    Task<TasksListResult?> ListMyTasksAsync(bool includeDone, CancellationToken cancellationToken = default);
    Task<TaskDetail?> MoveTaskAsync(Guid taskId, string status, uint? rowVersion, CancellationToken cancellationToken = default);
    Task<OpenTaskResult?> GetMyOpenTaskAsync(CancellationToken cancellationToken = default);
    Task<bool> PauseMyOpenTaskAsync(CancellationToken cancellationToken = default);
    Task<bool> ResumeMyOpenTaskAsync(CancellationToken cancellationToken = default);
    Task<bool> CloseOpenTaskOnIdleRejectAsync(CancellationToken cancellationToken = default);
    Task<NotificationsListResult?> ListMyNotificationsAsync(bool unreadOnly, int take, CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<ProjectListResult?> ListProjectsAsync(bool activeOnly, CancellationToken cancellationToken = default);
}

public sealed class TasksListResult
{
    public List<TaskDetail> Tasks { get; init; } = [];
    public int TodoCount { get; init; }
    public int InProgressCount { get; init; }
    public int DoneCount { get; init; }
    public long TotalSecondsWorked { get; init; }
}

public sealed class TaskDetail
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectColor { get; init; } = "#4A9FFF";
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? AssignedUserId { get; init; }
    public string? AssignedUserDisplayName { get; init; }
    public string Priority { get; init; } = "Medium";
    public DateTime? DueDate { get; init; }
    public double Position { get; init; }
    public long TotalSecondsWorked { get; init; }
    public uint RowVersion { get; init; }
    public bool IsRunning { get; init; }
    public long? RunningSeconds { get; init; }
}

public sealed class OpenTaskResult
{
    public Guid TaskId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectColor { get; init; } = "#4A9FFF";
    public string TaskTitle { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public long PausedSeconds { get; init; }
    public bool IsPaused { get; init; }
    public long ElapsedSeconds { get; init; }
}

public sealed class NotificationsListResult
{
    public List<NotificationDetail> Notifications { get; init; } = [];
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
}

public sealed class NotificationDetail
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = "Generic";
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed class ProjectListResult
{
    public List<ProjectDetail> Projects { get; init; } = [];
    public int TotalCount { get; init; }
}

public sealed class ProjectDetail
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Color { get; init; } = "#4A9FFF";
    public string Status { get; init; } = "Active";
}
