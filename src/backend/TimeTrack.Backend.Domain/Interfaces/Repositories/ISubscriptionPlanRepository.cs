using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetByTierAsync(PlanTier tier, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
}
