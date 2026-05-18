using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class FeatureMonthlyRepository : IFeatureMonthlyRepository
{
    private readonly TimeTrackDbContext _context;

    public FeatureMonthlyRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<FeatureMonthly?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FeatureMonthlies.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<FeatureMonthly>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FeatureMonthlies.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FeatureMonthly entity, CancellationToken cancellationToken = default)
    {
        await _context.FeatureMonthlies.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FeatureMonthly entity, CancellationToken cancellationToken = default)
    {
        _context.FeatureMonthlies.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FeatureMonthly entity, CancellationToken cancellationToken = default)
    {
        _context.FeatureMonthlies.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeatureMonthly?> GetByUserAndMonthAsync(Guid userId, DateOnly monthStart, CancellationToken ct = default)
    {
        return await _context.FeatureMonthlies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.MonthStart == monthStart, ct);
    }

    public async Task<IEnumerable<FeatureMonthly>> GetByUserAndDateRangeAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await _context.FeatureMonthlies
            .IgnoreQueryFilters()
            .Where(f => f.UserId == userId && f.MonthStart >= start && f.MonthStart <= end)
            .OrderByDescending(f => f.MonthStart)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(FeatureMonthly entity, CancellationToken ct = default)
    {
        var existing = await GetByUserAndMonthAsync(entity.UserId, entity.MonthStart, ct);
        if (existing != null)
        {
            _context.FeatureMonthlies.Remove(existing);
        }

        await _context.FeatureMonthlies.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }
}
