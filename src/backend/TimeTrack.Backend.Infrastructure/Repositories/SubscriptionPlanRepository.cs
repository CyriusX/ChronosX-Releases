using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly TimeTrackDbContext _context;

    public SubscriptionPlanRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<SubscriptionPlan?> GetByTierAsync(PlanTier tier, CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Tier == tier, cancellationToken);
    }

    public async Task<SubscriptionPlan?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans
            .OrderBy(p => p.Tier)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        await _context.SubscriptionPlans.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        _context.SubscriptionPlans.Update(plan);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
