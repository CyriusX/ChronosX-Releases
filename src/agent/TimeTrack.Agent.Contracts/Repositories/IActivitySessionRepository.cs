using TimeTrack.Agent.Domain.Entities;

using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para sessões de atividade
/// </summary>
public interface IActivitySessionRepository
{
    /// <summary>
    /// Obtém sessões do dia para um usuário específico
    /// </summary>
    Task<IReadOnlyList<ActivitySession>> GetByDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém sessões do período especificado para um usuário específico
    /// </summary>
    Task<IReadOnlyList<ActivitySession>> GetByDateRangeAsync(Guid userId, DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a sessão mais recente de um usuário
    /// </summary>
    Task<ActivitySession?> GetMostRecentAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a sessão ativa atual de um usuário (se houver)
    /// Uma sessão ativa é aquela que terminou nos últimos 60 segundos
    /// </summary>
    Task<ActivitySession?> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default);

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
