namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// One row per Linear sync execution — surfaced in the Integrations settings
/// card so users can see recent sync results and diagnose failures.
/// </summary>
public sealed class LinearSyncHistory
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime FinishedAt { get; private set; }
    public long DurationMs { get; private set; }
    public int ProjectsCreated { get; private set; }
    public int ProjectsUpdated { get; private set; }
    public int TasksCreated { get; private set; }
    public int TasksUpdated { get; private set; }
    public int TasksSoftDeleted { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    private LinearSyncHistory() { }

    public static LinearSyncHistory CreateSuccess(
        Guid orgId,
        Guid userId,
        DateTime startedAt,
        DateTime finishedAt,
        int projectsCreated,
        int projectsUpdated,
        int tasksCreated,
        int tasksUpdated,
        int tasksSoftDeleted)
    {
        return new LinearSyncHistory
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
            ProjectsCreated = projectsCreated,
            ProjectsUpdated = projectsUpdated,
            TasksCreated = tasksCreated,
            TasksUpdated = tasksUpdated,
            TasksSoftDeleted = tasksSoftDeleted,
            Success = true
        };
    }

    public static LinearSyncHistory CreateFailure(
        Guid orgId,
        Guid userId,
        DateTime startedAt,
        DateTime finishedAt,
        string errorMessage)
    {
        return new LinearSyncHistory
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
