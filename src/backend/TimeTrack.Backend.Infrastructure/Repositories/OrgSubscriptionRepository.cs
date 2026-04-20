using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class OrgSubscriptionRepository : IOrgSubscriptionRepository
{
    private readonly TimeTrackDbContext _context;

    public OrgSubscriptionRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<OrgSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<OrgSubscription?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrgId == orgId, cancellationToken);
    }

    public async Task<OrgSubscription?> GetByOrgIdUnfilteredAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrgId == orgId, cancellationToken);
    }

    public async Task<OrgSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, cancellationToken);
    }

    public async Task<OrgSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.StripeCustomerId == stripeCustomerId, cancellationToken);
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.OrgId == orgId)
            .Select(s => new { s.Status, s.GracePeriodEnd, s.TrialEnd })
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return false;

        if (subscription.Status == SubscriptionStatus.Active)
            return true;

        if (subscription.Status == SubscriptionStatus.Trialing
            && (subscription.TrialEnd == null || subscription.TrialEnd.Value > DateTime.UtcNow))
            return true;

        if (subscription.Status == SubscriptionStatus.PastDue
            && subscription.GracePeriodEnd.HasValue
            && subscription.GracePeriodEnd.Value > DateTime.UtcNow)
            return true;

        return false;
    }

    public async Task AddAsync(OrgSubscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.OrgSubscriptions.AddAsync(subscription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrgSubscription subscription, CancellationToken cancellationToken = default)
    {
        _context.OrgSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrgSubscription>> GetPastDueExpiredAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.PastDue
                && s.GracePeriodEnd.HasValue
                && s.GracePeriodEnd.Value <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrgSubscription>> GetTrialingExpiredAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OrgSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Trialing
                && s.TrialEnd.HasValue
                && s.TrialEnd.Value <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }
}
