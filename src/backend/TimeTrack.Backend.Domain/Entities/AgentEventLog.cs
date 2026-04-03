namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Registro de evento do agent sincronizado (ações do usuário, eventos de sistema, erros)
/// </summary>
public sealed class AgentEventLog
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public Device? Device { get; private set; }

    private AgentEventLog() { }

    public static AgentEventLog Create(
        Guid id,
        Guid orgId,
        Guid deviceId,
        string eventType,
        string category,
        string severity,
        string message,
        string? metadataJson,
        DateTime timestampUtc,
        string idempotencyKey)
    {
        return new AgentEventLog
        {
            Id = id,
            OrgId = orgId,
            DeviceId = deviceId,
            EventType = eventType,
            Category = category,
            Severity = severity,
            Message = message,
            MetadataJson = metadataJson,
            TimestampUtc = timestampUtc,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }
}
