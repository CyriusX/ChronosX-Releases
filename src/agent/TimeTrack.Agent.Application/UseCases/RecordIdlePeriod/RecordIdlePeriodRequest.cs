namespace TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;

/// <summary>
/// Request para registrar um período de inatividade
/// </summary>
public sealed class RecordIdlePeriodRequest
{
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
}
