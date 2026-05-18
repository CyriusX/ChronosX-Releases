namespace TimeTrack.Backend.Application.PlatformEvents.DTOs;

public sealed class PlatformEventLogDto
{
    public Guid Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public sealed class PlatformHealthDto
{
    public string Status { get; init; } = "healthy";
    public string ChecksJson { get; init; } = "{}";
    public DateTime LastChangedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

