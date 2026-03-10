namespace TimeTrack.Agent.Domain.Events;

/// <summary>
/// Evento disparado quando o tracking é pausado
/// </summary>
public sealed record TrackingPaused : IDomainEvent
{
    /// <summary>
    /// Motivo da pausa
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Identificador de quem pausou (user_id ou "system")
    /// </summary>
    public string PausedBy { get; init; }

    /// <summary>
    /// Momento em que o evento ocorreu
    /// </summary>
    public DateTime OccurredAt { get; init; }

    public TrackingPaused(string reason, string pausedBy, DateTime? occurredAt = null)
    {
        Reason = reason ?? string.Empty;
        PausedBy = pausedBy ?? "system";
        OccurredAt = occurredAt ?? DateTime.UtcNow;
    }
}
