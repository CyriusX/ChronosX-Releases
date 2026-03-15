namespace TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;

/// <summary>
/// Response do registro de período de inatividade
/// </summary>
public sealed class RecordIdlePeriodResponse
{
    /// <summary>
    /// ID do período criado
    /// </summary>
    public Guid IdlePeriodId { get; init; }

    /// <summary>
    /// Duração do período
    /// </summary>
    public TimeSpan Duration { get; init; }
}
