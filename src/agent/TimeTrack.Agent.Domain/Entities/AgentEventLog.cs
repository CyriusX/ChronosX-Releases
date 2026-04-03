using TimeTrack.Agent.Domain.Common;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Registro de evento do agent (ação do usuário, evento de sistema, ou erro)
/// </summary>
public sealed class AgentEventLog : EntityBase
{
    public string EventType { get; }
    public string Category { get; }
    public string Severity { get; }
    public string Message { get; }
    public string? MetadataJson { get; }
    public DateTime TimestampUtc { get; }

    private AgentEventLog() { }

    public AgentEventLog(
        Guid id,
        string eventType,
        string category,
        string severity,
        string message,
        string? metadataJson)
        : base(id)
    {
        EventType = eventType;
        Category = category;
        Severity = severity;
        Message = message;
        MetadataJson = metadataJson;
        TimestampUtc = DateTime.UtcNow;
    }

    public static AgentEventLog Create(
        string eventType,
        string category,
        string severity,
        string message,
        string? metadataJson = null)
    {
        return new AgentEventLog(
            Guid.NewGuid(),
            eventType,
            category,
            severity,
            message,
            metadataJson);
    }
}
