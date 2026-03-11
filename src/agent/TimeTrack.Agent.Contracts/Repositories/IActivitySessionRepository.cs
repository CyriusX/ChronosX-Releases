using TimeTrack.Agent.Domain.Entities;

using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para sessões de atividade
/// </summary>
public interface IActivitySessionRepository
{
    /// <summary>
    /// Obtém sessões do dia
    /// </summary>
    Task<IReadOnlyList<ActivitySession>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém sessões do período especificado
    /// </summary>
    Task<IReadOnlyList<ActivitySession>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a sessão mais recente
    /// </summary>
    Task<ActivitySession?> GetMostRecentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a sessão ativa atual (se houver)
    /// </summary>
    Task<ActivitySession?> GetActiveSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva uma sessão
    /// </summary>
    Task SaveAsync(ActivitySession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva múltiplas sessões em batch
    /// </summary>
    Task SaveBatchAsync(IEnumerable<ActivitySession> sessions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva a sessão com outbox items em uma única transação
    /// </summary>
    Task SaveWithOutboxAsync(ActivitySession session, IEnumerable<OutboxItem> outboxItems, CancellationToken cancellationToken = default);
}
