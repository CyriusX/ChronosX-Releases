namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para consultas de relatórios otimizadas
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Obtém totais de atividade agregados por app para um dia específico
    /// </summary>
    Task<DailyActivityAggregate> GetDailyActivityAggregateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém total de segundos de idle para um dia específico
    /// </summary>
    Task<long> GetDailyIdleSecondsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém top apps por período ordenados por tempo total
    /// </summary>
    Task<IEnumerable<AppAggregate>> GetTopAppsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Agregado de atividade diária
/// </summary>
public sealed class DailyActivityAggregate
{
    public long TotalSeconds { get; init; }
    public DateTime? FirstActivity { get; init; }
    public DateTime? LastActivity { get; init; }
    public List<AppAggregate> Apps { get; init; } = [];
}

/// <summary>
/// Agregado por app
/// </summary>
public sealed class AppAggregate
{
    public string ProcessName { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int SessionCount { get; init; }
    public string? AppCategory { get; init; }
}
