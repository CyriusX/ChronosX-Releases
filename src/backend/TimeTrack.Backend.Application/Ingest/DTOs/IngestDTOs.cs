using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Ingest.DTOs;

/// <summary>
/// Request para ingestão de sessões de atividade
/// </summary>
public sealed class ActivitySessionIngestRequest
{
    public required IEnumerable<ActivitySessionItem> Items { get; init; }
}

/// <summary>
/// Item de sessão de atividade para ingestão
/// </summary>
public sealed class ActivitySessionItem
{
    public required Guid Id { get; init; }
    public required string ProcessName { get; init; }
    public string? WindowTitle { get; init; }
    public string? FilePath { get; init; }
    public string? AppCategory { get; init; }
    public string? AppSubcategory { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime EndedAt { get; init; }
    public required string IdempotencyKey { get; init; }
}

/// <summary>
/// Request para ingestão de períodos de inatividade
/// </summary>
public sealed class IdlePeriodIngestRequest
{
    public required IEnumerable<IdlePeriodItem> Items { get; init; }
}

/// <summary>
/// Item de período de inatividade para ingestão
/// </summary>
public sealed class IdlePeriodItem
{
    public required Guid Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime EndedAt { get; init; }
    public required string IdempotencyKey { get; init; }
}

/// <summary>
/// Resposta padrão para endpoints de ingestão
/// </summary>
public sealed class IngestResponse
{
    public int Processed { get; init; }
    public int Duplicates { get; init; }
    public IReadOnlyList<IngestError> Errors { get; init; } = Array.Empty<IngestError>();
}

/// <summary>
/// Erro durante ingestão de um item
/// </summary>
public sealed class IngestError
{
    public Guid? ItemId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request para ingestão de sessões de foco
/// </summary>
public sealed class FocusSessionIngestRequest
{
    public required IEnumerable<FocusSessionItem> Items { get; init; }
}

/// <summary>
/// Item de sessão de foco para ingestão
/// </summary>
public sealed class FocusSessionItem
{
    public required Guid Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public required int PlannedDurationMinutes { get; init; }
    public int? ActualDurationMinutes { get; init; }
    public required string Status { get; init; } // "InProgress", "Completed", "Cancelled"
    public int? FocusScore { get; init; }
    public required string IdempotencyKey { get; init; }
}
