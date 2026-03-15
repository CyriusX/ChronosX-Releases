using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para repositório de sessões de foco
///
/// ISP: Interface segregada com apenas operações necessárias
/// DIP: Abstração no domínio, implementação na infraestrutura
/// </summary>
public interface IFocusSessionRepository : IRepository<FocusSession>
{
    /// <summary>
    /// Obtém sessões de foco de um usuário em uma data específica
    /// </summary>
    Task<IEnumerable<FocusSession>> GetByUserIdAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém sessões de foco de um usuário em um período
    /// </summary>
    Task<IEnumerable<FocusSession>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todas as sessões de uma organização em uma data
    /// </summary>
    Task<IEnumerable<FocusSession>> GetByOrgIdAndDateAsync(
        Guid orgId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se existe sessão com a idempotency key
    /// </summary>
    Task<bool> ExistsByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o total de minutos de foco de um usuário em uma data
    /// </summary>
    Task<int> GetTotalFocusMinutesByUserAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
