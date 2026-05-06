using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para períodos de inatividade
/// </summary>
public interface IIdlePeriodRepository
{
    Task<IdlePeriod?> GetByIdAsync(Guid idlePeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdlePeriod>> GetByDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdlePeriod>> GetByDateRangeAsync(Guid userId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task SaveAsync(IdlePeriod period, CancellationToken cancellationToken = default);
    Task SaveBatchAsync(IEnumerable<IdlePeriod> periods, CancellationToken cancellationToken = default);
    Task UpdateAsync(IdlePeriod period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva o período de inatividade junto com os itens do outbox em uma única transação
    /// </summary>
    Task SaveWithOutboxAsync(IdlePeriod period, IEnumerable<OutboxItem> outboxItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o período de inatividade junto com os itens do outbox em uma única transação
    /// </summary>
    Task UpdateWithOutboxAsync(IdlePeriod period, IEnumerable<OutboxItem> outboxItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all idle periods older than the given cutoff date.
    /// Used to keep SQLite lean — past data is in the cloud.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
