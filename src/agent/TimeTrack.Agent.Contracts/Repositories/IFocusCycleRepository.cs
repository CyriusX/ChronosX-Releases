using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para ciclos de foco
///
/// SOLID:
/// - SRP: Apenas persistência de ciclos de foco
/// - ISP: Interface coesa com métodos relacionados
/// - DIP: Abstração para permitir diferentes implementações
/// </summary>
public interface IFocusCycleRepository
{
    /// <summary>
    /// Salva um ciclo de foco
    /// </summary>
    Task SaveAsync(FocusCycle cycle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém ciclos de uma data específica
    /// </summary>
    Task<IReadOnlyList<FocusCycle>> GetByDateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o ciclo ativo atual (não finalizado)
    /// </summary>
    Task<FocusCycle?> GetActiveCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o último ciclo completado
    /// </summary>
    Task<FocusCycle?> GetLastCompletedCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta ciclos completados em uma data
    /// </summary>
    Task<int> CountCompletedCyclesAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém ciclos não sincronizados
    /// </summary>
    Task<IReadOnlyList<FocusCycle>> GetUnsyncedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes completed and synced focus cycles older than the cutoff date.
    /// Only removes cycles that have been synced to the cloud.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
