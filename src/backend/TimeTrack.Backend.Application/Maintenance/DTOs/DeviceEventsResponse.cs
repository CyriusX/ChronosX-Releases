namespace TimeTrack.Backend.Application.Maintenance.DTOs;

public sealed class DeviceEventsResponse
{
    public IReadOnlyList<DeviceEventItem> Events { get; init; } = Array.Empty<DeviceEventItem>();
    public int TotalCount { get; init; }
}

public sealed class DeviceEventItem
{
    public Guid Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTime Timestamp { get; init; }
}
