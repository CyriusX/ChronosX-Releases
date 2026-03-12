namespace TimeTrack.Backend.Application.Audit.DTOs;

/// <summary>
/// Resposta paginada de audit logs
/// </summary>
public sealed class AuditLogListResponse
{
    public IEnumerable<AuditLogItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

/// <summary>
/// Item de audit log na resposta
/// </summary>
public sealed class AuditLogItem
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserDisplayName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string? Metadata { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Filtros para consulta de audit logs
/// </summary>
public sealed class AuditLogFilterRequest
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 50;
    public string? Action { get; init; }
    public Guid? UserId { get; init; }
}
