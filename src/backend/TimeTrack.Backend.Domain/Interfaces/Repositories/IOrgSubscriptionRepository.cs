using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IOrgSubscriptionRepository
{
    Task<OrgSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrgSubscription?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<OrgSubscription?> GetByOrgIdUnfilteredAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<OrgSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken = default);
    Task<OrgSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSubscriptionAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task AddAsync(OrgSubscription subscription, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrgSubscription subscription, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrgSubscription>> GetPastDueExpiredAsync(CancellationToken cancellationToken = default);
}
