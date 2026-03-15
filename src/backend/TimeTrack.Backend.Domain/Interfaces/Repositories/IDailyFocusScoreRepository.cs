using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para repositório de scores de foco diários
///
/// ISP: Interface segregada com apenas operações necessárias
/// DIP: Abstração no domínio, implementação na infraestrutura
/// </summary>
public interface IDailyFocusScoreRepository : IRepository<DailyFocusScore>
{
    /// <summary>
    /// Obtém o score de foco de um usuário para uma data específica
    /// </summary>
    Task<DailyFocusScore?> GetByUserIdAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os scores de uma organização em uma data específica
    /// </summary>
    Task<IEnumerable<DailyFocusScore>> GetByOrgIdAndDateAsync(
        Guid orgId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os scores de um usuário em um período
    /// </summary>
    Task<IEnumerable<DailyFocusScore>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os scores de uma organização em um período
    /// </summary>
    Task<IEnumerable<DailyFocusScore>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert: cria ou atualiza um score diário (idempotente)
    /// </summary>
    Task<DailyFocusScore> UpsertAsync(
        Guid orgId,
        Guid userId,
        DateOnly date,
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        short focusScore,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert extended: cria ou atualiza um score diário com todos os campos
    /// </summary>
    Task<DailyFocusScore> UpsertAsync(
        Guid orgId,
        Guid userId,
        DateOnly date,
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        int longFocusBlockCount,
        short focusScore,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se existe score para um usuário em uma data
    /// </summary>
    Task<bool> ExistsAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a média de focus score de um usuário em um período
    /// </summary>
    Task<double> GetAverageScoreByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém usuários com activity_sessions mas sem focus_score calculado para uma data
    /// </summary>
    Task<IEnumerable<(Guid UserId, Guid OrgId)>> GetUsersNeedingScoreCalculationAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
