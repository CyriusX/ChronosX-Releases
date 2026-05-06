namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa uma sessão de atividade sincronizada do Agent
/// </summary>
public sealed class ActivitySession
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public string ProcessName { get; private set; } = string.Empty;
    public string? WindowTitle { get; private set; }
    public string? FilePath { get; private set; }
    public string? AppCategory { get; private set; }
    public string? AppSubcategory { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime EndedAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public Guid? ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }

    // Navigation properties
    public Device? Device { get; private set; }
    public User? User { get; private set; }
    public Project? Project { get; private set; }
    public ProjectTask? Task { get; private set; }

    private ActivitySession() { }

    public static ActivitySession Create(
        Guid id,
        Guid orgId,
        Guid deviceId,
        Guid userId,
        string processName,
        string? windowTitle,
        string? appCategory,
        DateTime startedAt,
        DateTime endedAt,
        string idempotencyKey,
        string? filePath = null,
        string? appSubcategory = null)
    {
        if (string.IsNullOrWhiteSpace(processName))
            throw new ArgumentException("Process name is required", nameof(processName));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        if (endedAt < startedAt)
            throw new ArgumentException("End time must be after start time");

        return new ActivitySession
        {
            Id = id,
            OrgId = orgId,
            DeviceId = deviceId,
            UserId = userId,
            ProcessName = processName,
            WindowTitle = windowTitle,
            FilePath = filePath,
            AppCategory = appCategory,
            AppSubcategory = appSubcategory,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationSeconds = (int)(endedAt - startedAt).TotalSeconds,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Stamps the session with the project/task that was in progress when it was recorded.
    /// Called by the ingest pipeline when an open TaskTimeEntry overlaps the session.
    /// </summary>
    public void LinkToTask(Guid projectId, Guid taskId)
    {
        ProjectId = projectId;
        TaskId = taskId;
    }

    public void ClearTaskLink()
    {
        ProjectId = null;
        TaskId = null;
    }

    /// <summary>
    /// Extends the session to a new end time
    /// Used during consolidation to merge consecutive sessions
    /// </summary>
    public void Extend(DateTime newEndedAt)
    {
        if (newEndedAt <= StartedAt)
            throw new ArgumentException("New end time must be after start time", nameof(newEndedAt));

        if (newEndedAt < EndedAt)
            throw new ArgumentException("New end time must be after current end time", nameof(newEndedAt));

        EndedAt = newEndedAt;
        DurationSeconds = (int)(EndedAt - StartedAt).TotalSeconds;
    }
}
