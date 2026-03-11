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
        return await _context.ActivitySessions
            .Where(a => a.UserId == userId && a.StartedAt >= startDate && a.EndedAt <= endDate)
            .OrderByDescending(a => a.StartedAt)
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
        return await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.StartedAt >= startDate && a.EndedAt <= endDate)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }
}
