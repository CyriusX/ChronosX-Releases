using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para o outbox pattern (Transactional Outbox)
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Obtém itens pendentes de sync (sent_at IS NULL e next_attempt_utc <= now)
    /// </summary>
    Task<IReadOnlyList<OutboxItem>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona um novo item ao outbox
    /// </summary>
    Task AddAsync(OutboxItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona múltiplos items ao outbox na mesma transação
    /// </summary>
    Task AddBatchAsync(IEnumerable<OutboxItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca itens como enviados com sucesso
    /// </summary>
    Task MarkAsSentAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca um item como falhou e atualiza attempt_count e next_attempt_utc
    /// </summary>
    Task MarkAsFailedAsync(
        Guid id,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove old sent items (para cleanup/retention)
    /// </summary>
    Task<int> RemoveSentOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um item pelo ID
    /// </summary>
    Task<OutboxItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se exists pending items
    /// </summary>
    Task<bool> HasPendingItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém itens já enviados (para determinar último sync)
    /// </summary>
    Task<IReadOnlyList<OutboxItem>> GetSentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redefine itens pendentes que estão presos em backoff exponencial,
    /// tornando-os imediatamente elegíveis para a próxima tentativa de sync.
    /// Útil para diagnóstico e recuperação manual após investigação.
    /// </summary>
    /// <returns>Número de itens redefinidos</returns>
    Task<int> ResetStuckItemsAsync(CancellationToken cancellationToken = default);
}
