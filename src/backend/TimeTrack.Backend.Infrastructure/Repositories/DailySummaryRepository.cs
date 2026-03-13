using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de resumos diários pré-calculados
/// </summary>
public sealed class DailySummaryRepository : IDailySummaryRepository
{
    private readonly TimeTrackDbContext _context;

    public DailySummaryRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<DailySummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DailySummaries.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<DailySummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DailySummaries.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DailySummary entity, CancellationToken cancellationToken = default)
    {
        await _context.DailySummaries.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DailySummary entity, CancellationToken cancellationToken = default)
    {
        _context.DailySummaries.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DailySummary entity, CancellationToken cancellationToken = default)
    {
        _context.DailySummaries.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DailySummary?> GetByUserIdAndDateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var dateOnly = date.Date;
        return await _context.DailySummaries
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == dateOnly, cancellationToken);
    }

    public async Task<IEnumerable<DailySummary>> GetByOrgIdAndDateRangeAsync(
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        return await _context.DailySummaries
            .Where(d => d.OrgId == orgId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DailySummary>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        return await _context.DailySummaries
            .Where(d => d.UserId == userId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<DailySummary> UpsertAsync(
        Guid orgId,
        Guid userId,
        DateTime date,
        int totalActiveSeconds,
        int totalIdleSeconds,
        int sessionCount,
        CancellationToken cancellationToken = default)
    {
        var dateOnly = date.Date;
        var existing = await GetByUserIdAndDateAsync(userId, dateOnly, cancellationToken);

        if (existing != null)
        {
            existing.Update(totalActiveSeconds, totalIdleSeconds, sessionCount);
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var summary = DailySummary.Create(
            Guid.NewGuid(),
            orgId,
            userId,
            dateOnly,
            totalActiveSeconds,
            totalIdleSeconds,
            sessionCount);

        await _context.DailySummaries.AddAsync(summary, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return summary;
    }

    public async Task<IEnumerable<DateTime>> GetMissingSummaryDatesAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        // Get all dates that have summaries
        var existingDates = await _context.DailySummaries
            .Where(d => d.UserId == userId && d.Date >= start && d.Date <= end)
            .Select(d => d.Date)
            .ToListAsync(cancellationToken);

        // Generate all dates in range
        var allDates = new List<DateTime>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            allDates.Add(date);
        }

        // Return missing dates
        return allDates.Except(existingDates);
    }
}
