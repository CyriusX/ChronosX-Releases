using Microsoft.Extensions.Configuration;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Infrastructure.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly IOrgUsageRecordRepository _usageRepository;
    private readonly IConfiguration _configuration;

    public SubscriptionService(
        IOrgSubscriptionRepository subscriptionRepository,
        IOrgUsageRecordRepository usageRepository,
        IConfiguration configuration)
    {
        _subscriptionRepository = subscriptionRepository;
        _usageRepository = usageRepository;
        _configuration = configuration;
    }

    public async Task<SubscriptionCheckResult> CheckSubscriptionAccessAsync(Guid orgId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, ct);

        // Orgs without a subscription row (pre-billing migration, failed trial creation, etc.)
        // get "trial_required" access — they can use core features but the UI should prompt
        // them to pick a plan. This is distinct from "none", which means an existing
        // subscription lapsed and is a blocking state.
        if (subscription is null)
        {
            return new SubscriptionCheckResult { HasAccess = true, Status = "trial_required" };
        }

        if (subscription.Plan is null)
        {
            return new SubscriptionCheckResult { HasAccess = false, Status = subscription.Status.ToString().ToLowerInvariant() };
        }

        return new SubscriptionCheckResult
        {
            HasAccess = subscription.HasActiveAccess(),
            Status = subscription.Status.ToString().ToLowerInvariant(),
            PlanTier = subscription.Plan.Tier.ToString().ToLowerInvariant(),
            PlanName = subscription.Plan.Name,
            IsInGracePeriod = subscription.IsInGracePeriod(),
            GracePeriodEnd = subscription.GracePeriodEnd,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd
        };
    }

    public async Task<bool> IsFeatureEnabledAsync(Guid orgId, string feature, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, ct);

        if (subscription is null || subscription.Plan is null || !subscription.HasActiveAccess())
            return false;

        return subscription.Plan.IsFeatureEnabled(feature);
    }

    public async Task EnforceSeatLimitAsync(Guid orgId, int requestedSeats, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, ct);

        if (subscription is null || !subscription.HasActiveAccess())
            throw new SubscriptionRequiredException("Active subscription required to add users");

        if (requestedSeats > subscription.Quantity)
        {
            throw new SubscriptionLimitExceededException(
                "max_users",
                $"Seat limit of {subscription.Quantity} exceeded. Requested: {requestedSeats}");
        }
    }
}
