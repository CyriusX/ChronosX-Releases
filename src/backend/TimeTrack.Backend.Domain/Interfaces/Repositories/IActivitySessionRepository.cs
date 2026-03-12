using System.Runtime.CompilerServices;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IActivitySessionRepository : IRepository<ActivitySession>
{
    Task<IEnumerable<ActivitySession>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<ActivitySession?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ActivitySession>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna dados agregados por data e app para export CSV com streaming
    /// </summary>
    IAsyncEnumerable<ActivitySession> GetSessionsForExportAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
