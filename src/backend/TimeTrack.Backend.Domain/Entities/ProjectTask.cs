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

    public Project? Project { get; private set; }
    public User? AssignedUser { get; private set; }

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
}
