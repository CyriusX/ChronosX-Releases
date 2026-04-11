using System.ComponentModel.DataAnnotations;

namespace TimeTrack.Backend.Application.Tasks.DTOs;

public sealed class CreateTaskRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public Guid? AssignedUserId { get; init; }

    public string Priority { get; init; } = "Medium"; // Low | Medium | High

    public DateTime? DueDate { get; init; }
}

public sealed class UpdateTaskRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public Guid? AssignedUserId { get; init; }

    public string Priority { get; init; } = "Medium";

    public DateTime? DueDate { get; init; }
}

public sealed class MoveTaskRequest
{
    [Required]
    public string Status { get; init; } = string.Empty; // Todo | InProgress | Done

    public double? Position { get; init; }

    public uint? RowVersion { get; init; }
}

public sealed class TaskResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectColor { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? AssignedUserId { get; init; }
    public string? AssignedUserDisplayName { get; init; }
    public string Priority { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public double Position { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? MovedToInProgressAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public long TotalSecondsWorked { get; init; }
    public uint RowVersion { get; init; }
    public bool IsRunning { get; init; }
    public long? RunningSeconds { get; init; }

    // ── Linear sync metadata (nullable — populated only for Linear-sourced tasks) ──
    public bool IsLinearSourced { get; init; }
    public string? LinearIssueIdentifier { get; init; }
    public string? LinearUrl { get; init; }
    public string? LinearStateName { get; init; }
}

public sealed class ListTasksResponse
{
    public List<TaskResponse> Tasks { get; init; } = [];
    public int TodoCount { get; init; }
    public int InProgressCount { get; init; }
    public int DoneCount { get; init; }
    public long TotalSecondsWorked { get; init; }
}

public sealed class OpenTaskResponse
{
    public Guid TaskId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectColor { get; init; } = string.Empty;
    public string TaskTitle { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public long PausedSeconds { get; init; }
    public bool IsPaused { get; init; }
    public long ElapsedSeconds { get; init; }
}
