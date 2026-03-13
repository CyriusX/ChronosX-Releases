using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para repositório de resumos diários pré-calculados
/// </summary>
public interface IDailySummaryRepository : IRepository<DailySummary>
{
    /// <summary>
    /// Obtém o resumo diário de um usuário para uma data específica
    /// </summary>
    Task<DailySummary?> GetByUserIdAndDateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os resumos de uma organização em um período
    /// </summary>
    Task<IEnumerable<DailySummary>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os resumos de um usuário em um período
    /// </summary>
    Task<IEnumerable<DailySummary>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert: cria ou atualiza um resumo diário
    /// </summary>
    Task<DailySummary> UpsertAsync(
        Guid orgId,
        Guid userId,
        DateTime date,
        int totalActiveSeconds,
        int totalIdleSeconds,
        int sessionCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém datas que ainda não possuem resumo calculado
    /// </summary>
    Task<IEnumerable<DateTime>> GetMissingSummaryDatesAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
