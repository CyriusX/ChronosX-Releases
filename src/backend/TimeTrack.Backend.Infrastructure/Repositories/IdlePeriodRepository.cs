using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class IdlePeriodRepository : IIdlePeriodRepository
{
    private readonly TimeTrackDbContext _context;

    public IdlePeriodRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<IdlePeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IdlePeriods.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<IdlePeriod>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IdlePeriods.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(IdlePeriod entity, CancellationToken cancellationToken = default)
    {
        await _context.IdlePeriods.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(IdlePeriod entity, CancellationToken cancellationToken = default)
    {
        _context.IdlePeriods.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(IdlePeriod entity, CancellationToken cancellationToken = default)
    {
        _context.IdlePeriods.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IdlePeriod?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.IdlePeriods
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IEnumerable<IdlePeriod>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.IdlePeriods
            .Where(i => i.UserId == userId && i.StartedAt >= startDate && i.EndedAt <= endDate)
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync(cancellationToken);
    }
}
