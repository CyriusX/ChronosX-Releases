using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class ActivitySessionRepository : IActivitySessionRepository
{
    private readonly TimeTrackDbContext _context;

    public ActivitySessionRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<ActivitySession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ActivitySessions.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<ActivitySession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ActivitySessions.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ActivitySession entity, CancellationToken cancellationToken = default)
    {
        await _context.ActivitySessions.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ActivitySession entity, CancellationToken cancellationToken = default)
    {
        _context.ActivitySessions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ActivitySession entity, CancellationToken cancellationToken = default)
    {
        _context.ActivitySessions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ActivitySession>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Overlap query: include sessions that OVERLAP with [startDate, endDate), not just
        // ones that start within it. Cross-midnight sessions that start just before startDate
        // and end within the range are included so the timeline shows them.
        return await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt < endDate && a.EndedAt >= startDate)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ActivitySession?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivitySessions
            .FirstOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IEnumerable<ActivitySession>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Get sessions that overlap with the date range (started before end of range AND ended after start of range)
        // This captures sessions that:
        // - Started within the range
        // - Started before and ended within the range
        // - Span across the range
        return await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.StartedAt < endDate && a.EndedAt >= startDate)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retorna sessões para export CSV com streaming
    /// Usa IAsyncEnumerable para não carregar todos os dados em memória
    /// </summary>
    public async IAsyncEnumerable<ActivitySession> GetSessionsForExportAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Query raw sessions with streaming
        var sessions = _context.ActivitySessions
            // Unit tests construct the DbContext without a CurrentUserContext,
            // so global OrgId query filters can throw. Export endpoints already
            // validate authorization at the API layer.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= startDate && a.EndedAt <= endDate)
            .OrderBy(a => a.StartedAt)
            .AsAsyncEnumerable();

        await foreach (var session in sessions.WithCancellation(cancellationToken))
        {
            yield return session;
        }
    }

    /// <summary>
    /// Adiciona em lote de sessões de forma eficiente
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<ActivitySession> sessions, CancellationToken cancellationToken = default)
    {
        await _context.ActivitySessions.AddRangeAsync(sessions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UpdateCategoryByProcessNameAsync(
        Guid orgId,
        string processName,
        string newAppCategory,
        string? newAppSubcategory,
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL for efficient bulk update — avoids loading all entities into memory.
        // Case-insensitive match on process_name, scoped to the specific organization.
        // Table and column names use snake_case (Postgres convention from EF configuration).
        var updated = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE activity_sessions
              SET app_category = {0}, app_subcategory = {1}
              WHERE org_id = {2}
                AND LOWER(process_name) = LOWER({3})",
            [newAppCategory, newAppSubcategory ?? "unknown", orgId, processName],
            cancellationToken);

        return updated;
    }
}
