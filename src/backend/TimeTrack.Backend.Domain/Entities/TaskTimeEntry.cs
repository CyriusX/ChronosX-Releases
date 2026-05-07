using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// A wall-clock interval representing time the user spent on a task.
/// Open entries (EndedAt = null) are the "currently in progress" timer.
/// At most one open entry per user at any time.
/// </summary>
public sealed class TaskTimeEntry
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public long DurationSeconds { get; private set; }
    public long PausedSeconds { get; private set; }
    public DateTime? PausedAt { get; private set; }
    public TaskTimeEntrySource Source { get; private set; }

    public ProjectTask? Task { get; private set; }
    public User? User { get; private set; }

    private TaskTimeEntry() { }

    public static TaskTimeEntry Open(
        Guid orgId,
        Guid taskId,
        Guid userId,
        TaskTimeEntrySource source = TaskTimeEntrySource.Kanban)
    {
        return new TaskTimeEntry
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TaskId = taskId,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            EndedAt = null,
            DurationSeconds = 0,
            PausedSeconds = 0,
            PausedAt = null,
            Source = source
        };
    }

    /// <summary>
    /// Closes the entry. Returns the duration that was just added (excludes paused time).
    /// </summary>
    public long Close(DateTime? endedAt = null)
    {
        if (EndedAt is not null)
            return 0;

        // If the entry was paused at the moment of closing, fold the open pause into PausedSeconds.
        var now = endedAt ?? DateTime.UtcNow;
        if (PausedAt is not null)
        {
            PausedSeconds += (long)(now - PausedAt.Value).TotalSeconds;
            PausedAt = null;
        }

        EndedAt = now;
        var totalElapsed = (long)(EndedAt.Value - StartedAt).TotalSeconds;
        DurationSeconds = Math.Max(0, totalElapsed - PausedSeconds);
        return DurationSeconds;
    }

    public void Pause()
    {
        PauseAt(DateTime.UtcNow);
    }

    public void PauseAt(DateTime pausedAtUtc)
    {
        if (EndedAt is not null) return;
        if (PausedAt is not null) return;

        var now = DateTime.UtcNow;
        var effective = pausedAtUtc.Kind == DateTimeKind.Utc
            ? pausedAtUtc
            : DateTime.SpecifyKind(pausedAtUtc, DateTimeKind.Utc);

        if (effective > now) effective = now;
        if (effective < StartedAt) effective = StartedAt;

        PausedAt = effective;
    }

    public void Resume()
    {
        if (EndedAt is not null) return;
        if (PausedAt is null) return;
        PausedSeconds += (long)(DateTime.UtcNow - PausedAt.Value).TotalSeconds;
        PausedAt = null;
    }

    public bool IsOpen => EndedAt is null;
    public bool IsPaused => PausedAt is not null;
}
