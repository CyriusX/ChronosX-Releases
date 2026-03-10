using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa um período de inatividade do usuário
/// </summary>
public sealed class IdlePeriod : EntityBase
{
    /// <summary>
    /// Intervalo de tempo do período de inatividade
    /// </summary>
    public TimeRange Period { get; private set; }

    /// <summary>
    /// Limiar em segundos que define o início da inatividade
    /// </summary>
    public int ThresholdSeconds { get; }

    /// <summary>
    /// Indica se o período foi detectado automaticamente pelo sistema
    /// </summary>
    public bool IsSystemDetected { get; }

    private IdlePeriod() { }

    public IdlePeriod(
        Guid id,
        TimeRange period,
        int thresholdSeconds,
        bool isSystemDetected = true)
        : base(id)
    {
        if (thresholdSeconds <= 0)
            throw new DomainException("INVALID_THRESHOLD", "O limiar de inatividade deve ser maior que zero.");

        Period = period ?? throw new ArgumentNullException(nameof(period));
        ThresholdSeconds = thresholdSeconds;
        IsSystemDetected = isSystemDetected;
    }

    /// <summary>
    /// Cria um novo período de inatividade com ID gerado automaticamente
    /// </summary>
    public static IdlePeriod Create(
        TimeRange period,
        int thresholdSeconds,
        bool isSystemDetected = true)
    {
        return new IdlePeriod(Guid.NewGuid(), period, thresholdSeconds, isSystemDetected);
    }

    /// <summary>
    /// Duração do período de inatividade
    /// </summary>
    public TimeSpan Duration => Period.Duration;

    /// <summary>
    /// Verifica se o período de inatividade excede um determinado limiar
    /// </summary>
    public bool ExceedsThreshold(int seconds)
    {
        return Duration.TotalSeconds > seconds;
    }

    public override string ToString()
        => $"Idle[{Id}] {Period} (threshold: {ThresholdSeconds}s)";
}
