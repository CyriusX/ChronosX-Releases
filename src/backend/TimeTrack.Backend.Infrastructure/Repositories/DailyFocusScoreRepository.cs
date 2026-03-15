using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de scores de foco diários
///
/// SRP: Apenas persiste e recupera DailyFocusScore
/// DIP: Implementa a interface definida no domínio
/// </summary>
public sealed class DailyFocusScoreRepository : IDailyFocusScoreRepository
{
    private readonly TimeTrackDbContext _context;

    public DailyFocusScoreRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    // ============================================================================
    // IRepository<T> Implementation
    // ============================================================================

    public async Task<DailyFocusScore?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<DailyFocusScore>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DailyFocusScore entity, CancellationToken cancellationToken = default)
    {
        await _context.DailyFocusScores.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DailyFocusScore entity, CancellationToken cancellationToken = default)
    {
        _context.DailyFocusScores.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DailyFocusScore entity, CancellationToken cancellationToken = default)
    {
        _context.DailyFocusScores.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ============================================================================
    // IDailyFocusScoreRepository Implementation
    // ============================================================================

    public async Task<DailyFocusScore?> GetByUserIdAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.UserId == userId && d.Date == date,
                cancellationToken);
    }

    public async Task<IEnumerable<DailyFocusScore>> GetByOrgIdAndDateAsync(
        Guid orgId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId && d.Date == date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DailyFocusScore>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= startDate && d.Date <= endDate)
            .OrderByDescending(d => d.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DailyFocusScore>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId && d.Date >= startDate && d.Date <= endDate)
            .OrderByDescending(d => d.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<DailyFocusScore> UpsertAsync(
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
        CancellationToken cancellationToken = default)
    {
        // Delegate to extended version with longFocusBlockCount = 0
        return await UpsertAsync(
            orgId, userId, date,
            totalTrackedMs, focusTimeMs, distractionMs,
            distractionCount, pauseCount, idleCount, 0,
            focusScore, deviceId, cancellationToken);
    }

    public async Task<DailyFocusScore> UpsertAsync(
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
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByUserIdAndDateAsync(userId, date, cancellationToken);

        if (existing != null)
        {
            existing.Recalculate(
                totalTrackedMs,
                focusTimeMs,
                distractionMs,
                distractionCount,
                pauseCount,
                idleCount,
                longFocusBlockCount,
                focusScore);

            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var score = DailyFocusScore.Create(
            Guid.NewGuid(),
            orgId,
            userId,
            date,
            totalTrackedMs,
            focusTimeMs,
            distractionMs,
            distractionCount,
            pauseCount,
            idleCount,
            longFocusBlockCount,
            focusScore,
            deviceId);

        await _context.DailyFocusScores.AddAsync(score, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return score;
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .AnyAsync(d => d.UserId == userId && d.Date == date, cancellationToken);
    }

    public async Task<double> GetAverageScoreByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var scores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= startDate && d.Date <= endDate)
            .Select(d => (double)d.FocusScore)
            .ToListAsync(cancellationToken);

        return scores.Count == 0 ? 0 : scores.Average();
    }

    public async Task<IEnumerable<(Guid UserId, Guid OrgId)>> GetUsersNeedingScoreCalculationAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Find users with activity sessions but no focus score for the date
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = startOfDay.AddDays(1);

        var usersWithActivity = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= startOfDay && a.StartedAt < endOfDay)
            .GroupBy(a => new { a.UserId, a.OrgId })
            .Select(g => new { g.Key.UserId, g.Key.OrgId })
            .ToListAsync(cancellationToken);

        var usersWithScore = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date == date)
            .Select(d => d.UserId)
            .ToListAsync(cancellationToken);

        return usersWithActivity
            .Where(u => !usersWithScore.Contains(u.UserId))
            .Select(u => (u.UserId, u.OrgId));
    }
}
