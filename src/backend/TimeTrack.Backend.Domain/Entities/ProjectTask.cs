using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// A kanban card belonging to a project. Tracks status, assignment, and accumulated work time.
/// </summary>
public sealed class ProjectTask
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? DueDate { get; private set; }
    public double Position { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? MovedToInProgressAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long TotalSecondsWorked { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public uint RowVersion { get; private set; }

    // ── Linear sync (nullable — only populated when mirrored from Linear) ──
    public string? LinearIssueId { get; private set; }
    public string? LinearIssueIdentifier { get; private set; }
    public string? LinearUrl { get; private set; }
    public string? LinearStateId { get; private set; }
    public string? LinearStateName { get; private set; }
    public string? LinearTeamId { get; private set; }

    public Project? Project { get; private set; }
    public User? AssignedUser { get; private set; }

    public bool IsLinearSourced => LinearIssueId != null;

    private ProjectTask() { }

    public static ProjectTask Create(
        Guid orgId,
        Guid projectId,
        string title,
        Guid createdByUserId,
        Guid? assignedUserId = null,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null,
        double position = 0)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required", nameof(title));

        return new ProjectTask
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = projectId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Status = ProjectTaskStatus.Todo,
            AssignedUserId = assignedUserId,
            CreatedByUserId = createdByUserId,
            Priority = priority,
            DueDate = dueDate,
            Position = position,
            CreatedAt = DateTime.UtcNow,
            TotalSecondsWorked = 0
        };
    }

    public void UpdateDetails(string title, string? description, TaskPriority priority, DateTime? dueDate, Guid? assignedUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required", nameof(title));

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        DueDate = dueDate;
        AssignedUserId = assignedUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveTo(ProjectTaskStatus newStatus, double newPosition)
    {
        var now = DateTime.UtcNow;

        if (newStatus == ProjectTaskStatus.InProgress && Status != ProjectTaskStatus.InProgress)
        {
            if (MovedToInProgressAt is null)
                MovedToInProgressAt = now;
        }

        if (newStatus == ProjectTaskStatus.Done)
            CompletedAt = now;
        else if (Status == ProjectTaskStatus.Done && newStatus != ProjectTaskStatus.Done)
            CompletedAt = null;

        Status = newStatus;
        Position = newPosition;
        UpdatedAt = now;
    }

    public void AccumulateWorkedTime(long seconds)
    {
        if (seconds <= 0) return;
        TotalSecondsWorked += seconds;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt;
    }

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Linear sync helpers ──────────────────────────────────────────────

    public static ProjectTask CreateFromLinear(
        Guid orgId,
        Guid projectId,
        Guid createdByUserId,
        string title,
        string? description,
        TaskPriority priority,
        DateTime? dueDate,
        ProjectTaskStatus status,
        Guid? assignedUserId,
        double position,
        string linearIssueId,
        string? linearIssueIdentifier,
        string? linearUrl,
        string? linearStateId,
        string? linearStateName,
        string? linearTeamId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required", nameof(title));
        if (string.IsNullOrWhiteSpace(linearIssueId))
            throw new ArgumentException("Linear issue id is required", nameof(linearIssueId));

        var now = DateTime.UtcNow;
        return new ProjectTask
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = projectId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Status = status,
            AssignedUserId = assignedUserId,
            CreatedByUserId = createdByUserId,
            Priority = priority,
            DueDate = dueDate,
            Position = position,
            CreatedAt = now,
            UpdatedAt = now,
            MovedToInProgressAt = status == ProjectTaskStatus.InProgress ? now : null,
            CompletedAt = status == ProjectTaskStatus.Done ? now : null,
            TotalSecondsWorked = 0,
            LinearIssueId = linearIssueId.Trim(),
            LinearIssueIdentifier = linearIssueIdentifier?.Trim(),
            LinearUrl = linearUrl?.Trim(),
            LinearStateId = linearStateId?.Trim(),
            LinearStateName = linearStateName?.Trim(),
            LinearTeamId = linearTeamId?.Trim()
        };
    }

    /// <summary>
    /// Apply a snapshot from Linear. Does NOT trigger timer side-effects — callers must
    /// handle open TaskTimeEntry cleanup themselves before calling this.
    /// </summary>
    public void ApplyLinearSnapshot(
        string title,
        string? description,
        TaskPriority priority,
        DateTime? dueDate,
        ProjectTaskStatus status,
        Guid? assignedUserId,
        string? linearIssueIdentifier,
        string? linearUrl,
        string? linearStateId,
        string? linearStateName,
        string? linearTeamId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required", nameof(title));

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        DueDate = dueDate;
        AssignedUserId = assignedUserId;
        LinearIssueIdentifier = linearIssueIdentifier?.Trim();
        LinearUrl = linearUrl?.Trim();
        LinearStateId = linearStateId?.Trim();
        LinearStateName = linearStateName?.Trim();
        LinearTeamId = linearTeamId?.Trim();

        var now = DateTime.UtcNow;
        if (Status != status)
        {
            if (status == ProjectTaskStatus.InProgress && MovedToInProgressAt is null)
                MovedToInProgressAt = now;
            if (status == ProjectTaskStatus.Done)
                CompletedAt = now;
            else if (Status == ProjectTaskStatus.Done && status != ProjectTaskStatus.Done)
                CompletedAt = null;
            Status = status;
        }

        UpdatedAt = now;
    }

    public void UpdateLinearState(string? linearStateId, string? linearStateName)
    {
        LinearStateId = linearStateId?.Trim();
        LinearStateName = linearStateName?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
