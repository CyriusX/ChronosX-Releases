namespace TimeTrack.Backend.Application.Audit.DTOs;

public sealed class EvidenceAccessLogResponse
{
    public IEnumerable<EvidenceAccessLogItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class EvidenceAccessLogItem
{
    public Guid Id { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public string? ActorEmail { get; init; }
    public Guid? TargetUserId { get; init; }
    public string? TargetName { get; init; }
    public string? TargetEmail { get; init; }
    public Guid? EvidenceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public DateTime AccessedAt { get; init; }
    public string? IpAddress { get; init; }
}
