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
        _logger.LogInformation("SubscriptionStatusJob: checking for expired grace periods and trials");

        var pastDueExpired = await _subscriptionRepository.GetPastDueExpiredAsync();
        foreach (var subscription in pastDueExpired)
        {
            _logger.LogInformation(
                "SubscriptionStatusJob: transitioning org {OrgId} from PastDue to Unpaid (grace period expired)",
                subscription.OrgId);

            subscription.MarkUnpaid();
            await _subscriptionRepository.UpdateAsync(subscription);
        }

        var trialsExpired = await _subscriptionRepository.GetTrialingExpiredAsync();
        foreach (var subscription in trialsExpired)
        {
            _logger.LogInformation(
                "SubscriptionStatusJob: transitioning org {OrgId} from Trialing to Canceled (trial ended {TrialEnd:u})",
                subscription.OrgId,
                subscription.TrialEnd);

            subscription.ExpireTrial();
            await _subscriptionRepository.UpdateAsync(subscription);
        }

        _logger.LogInformation(
            "SubscriptionStatusJob: processed {PastDueCount} past-due and {TrialsCount} trial-expired subscriptions",
            pastDueExpired.Count,
            trialsExpired.Count);
    }
}
