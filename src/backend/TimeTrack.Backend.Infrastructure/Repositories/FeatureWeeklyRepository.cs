using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class FeatureWeeklyRepository : IFeatureWeeklyRepository
{
    private readonly TimeTrackDbContext _context;

    public FeatureWeeklyRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<FeatureWeekly?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FeatureWeeklies.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<FeatureWeekly>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FeatureWeeklies.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FeatureWeekly entity, CancellationToken cancellationToken = default)
    {
        await _context.FeatureWeeklies.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FeatureWeekly entity, CancellationToken cancellationToken = default)
    {
        _context.FeatureWeeklies.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FeatureWeekly entity, CancellationToken cancellationToken = default)
    {
        _context.FeatureWeeklies.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeatureWeekly?> GetByUserAndWeekAsync(Guid userId, DateOnly weekStart, CancellationToken ct = default)
    {
        return await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.WeekStart == weekStart, ct);
    }

    public async Task<IEnumerable<FeatureWeekly>> GetByUserAndDateRangeAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(f => f.UserId == userId && f.WeekStart >= start && f.WeekStart <= end)
            .OrderByDescending(f => f.WeekStart)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<FeatureWeekly>> GetByOrgAndWeekAsync(Guid orgId, DateOnly weekStart, CancellationToken ct = default)
    {
        return await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(f => f.OrgId == orgId && f.WeekStart == weekStart)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(FeatureWeekly entity, CancellationToken ct = default)
    {
        var existing = await GetByUserAndWeekAsync(entity.UserId, entity.WeekStart, ct);
        if (existing != null)
        {
            _context.FeatureWeeklies.Remove(existing);
        }

        await _context.FeatureWeeklies.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }
}
