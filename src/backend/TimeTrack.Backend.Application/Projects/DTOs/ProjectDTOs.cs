using System.ComponentModel.DataAnnotations;

namespace TimeTrack.Backend.Application.Projects.DTOs;

/// <summary>
/// Request para criar um novo projeto
/// </summary>
public sealed class CreateProjectRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [MaxLength(7)]
    public string? Color { get; init; }

    public bool? IsBillable { get; init; }

    [MaxLength(3)]
    public string? Currency { get; init; }

    public decimal? HourlyRate { get; init; }
}

/// <summary>
/// Request para atualizar um projeto
/// </summary>
public sealed class UpdateProjectRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [MaxLength(7)]
    public string? Color { get; init; }

    public bool? IsBillable { get; init; }

    [MaxLength(3)]
    public string? Currency { get; init; }

    public decimal? HourlyRate { get; init; }
}

/// <summary>
/// Response de um projeto
/// </summary>
public sealed class ProjectResponse
{
    public Guid Id { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Color { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool CanArchive { get; init; }
    public bool CanDelete { get; init; }
    public bool CanReactivate { get; init; }

    // ── Billable project fields ──
    public bool IsBillable { get; init; }
    public string? Currency { get; init; }
    public decimal? HourlyRate { get; init; }

    // ── Linear sync metadata ──
    public string SyncSource { get; init; } = "Local"; // "Local" | "Linear"
    public string? LinearProjectId { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}

/// <summary>
/// Response da listagem de projetos
/// </summary>
public sealed class ListProjectsResponse
{
    public List<ProjectResponse> Projects { get; init; } = [];
    public int TotalCount { get; init; }
}
