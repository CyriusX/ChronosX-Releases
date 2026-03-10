namespace TimeTrack.Agent.Domain.Events;

/// <summary>
/// Evento disparado quando o tracking é retomado
/// </summary>
public sealed record TrackingResumed : IDomainEvent
{
    /// <summary>
    /// Identificador de quem retomou (user_id ou "system")
    /// </summary>
    public string ResumedBy { get; init; }

    /// <summary>
    /// Momento em que o evento ocorreu
    /// </summary>
    public DateTime OccurredAt { get; init; }

    public TrackingResumed(string resumedBy, DateTime? occurredAt = null)
    {
        ResumedBy = resumedBy ?? "system";
        OccurredAt = occurredAt ?? DateTime.UtcNow;
    }
}
