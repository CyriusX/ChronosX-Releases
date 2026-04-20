using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class SubscriptionStatusJob : ISubscriptionStatusJob
{
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly ILogger<SubscriptionStatusJob> _logger;

    public SubscriptionStatusJob(
        IOrgSubscriptionRepository subscriptionRepository,
        ILogger<SubscriptionStatusJob> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("SubscriptionStatusJob: checking for expired grace periods");

        var expired = await _subscriptionRepository.GetPastDueExpiredAsync();

        foreach (var subscription in expired)
        {
            _logger.LogInformation(
                "SubscriptionStatusJob: transitioning org {OrgId} from PastDue to Unpaid (grace period expired)",
                subscription.OrgId);

            subscription.MarkUnpaid();
            await _subscriptionRepository.UpdateAsync(subscription);
        }

        _logger.LogInformation("SubscriptionStatusJob: processed {Count} expired subscriptions", expired.Count);
    }
}
