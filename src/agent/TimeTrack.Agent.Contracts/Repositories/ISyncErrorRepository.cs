using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Interface para repositório de erros de sincronização
/// </summary>
public interface ISyncErrorRepository
{
    /// <summary>
    /// Adiciona um novo erro de sync
    /// </summary>
    Task AddAsync(SyncError error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o último erro registrado
    /// </summary>
    Task<SyncError?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém erros dentro de um período
    /// </summary>
    Task<IEnumerable<SyncError>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta erros consecutivos desde o último sucesso
    /// </summary>
    Task<int> CountConsecutiveFailuresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa erros antigos (mais de X dias)
    /// </summary>
    Task CleanupOldErrorsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);
}
