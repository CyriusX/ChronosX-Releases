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

    /// <summary>
    /// Bulk-updates the AppCategory and AppSubcategory for all sessions in an org
    /// that match the given ProcessName (case-insensitive).
    /// Used when an admin changes an app's classification — retroactively applies to all historical sessions.
    /// </summary>
    Task<int> UpdateCategoryByProcessNameAsync(
        Guid orgId,
        string processName,
        string newAppCategory,
        string? newAppSubcategory,
        CancellationToken cancellationToken = default);
}
