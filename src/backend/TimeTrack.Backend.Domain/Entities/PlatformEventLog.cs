namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Platform-level events for SysAdmin maintenance visibility (not tied to a single device).
/// </summary>
public sealed class PlatformEventLog
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private PlatformEventLog() { }

    public static PlatformEventLog Create(
        Guid id,
        string eventType,
        string severity,
        string message,
        string? metadataJson,
        DateTime timestampUtc,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required", nameof(eventType));
        if (string.IsNullOrWhiteSpace(severity))
            throw new ArgumentException("Severity is required", nameof(severity));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        return new PlatformEventLog
        {
            Id = id,
            EventType = eventType.Trim(),
            Severity = severity.Trim(),
            Message = message.Trim(),
            MetadataJson = metadataJson,
            TimestampUtc = timestampUtc,
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

