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
}

/// <summary>
/// Response de um projeto
/// </summary>
public sealed class ProjectResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Color { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

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
