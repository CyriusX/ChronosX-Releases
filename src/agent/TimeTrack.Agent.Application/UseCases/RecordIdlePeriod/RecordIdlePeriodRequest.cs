namespace TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;

/// <summary>
/// Request para registrar um período de inatividade
/// </summary>
public sealed class RecordIdlePeriodRequest
{
    /// <summary>
    /// Optional explicit ID to use for the idle period.
    /// Used for live/placeholder idle that is updated in-place.
    /// </summary>
    public Guid? IdlePeriodId { get; init; }

    /// <summary>
    /// Momento em que o idle começou
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Momento em que o idle terminou
    /// </summary>
    public DateTime EndedAt { get; init; }

    /// <summary>
    /// Limiar de idle em segundos
    /// </summary>
    public int ThresholdSeconds { get; init; }

    /// <summary>
    /// Se foi detectado pelo sistema
    /// </summary>
    public bool IsSystemDetected { get; init; } = true;

    /// <summary>
    /// Whether to create an outbox item for cloud sync.
    /// Defaults to true for completed idle periods.
    /// </summary>
    public bool CreateOutbox { get; init; } = true;
}
