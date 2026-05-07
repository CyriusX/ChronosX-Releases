using MediatR;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Queries;

public sealed record GetSubscriptionStatusQuery : IRequest<SubscriptionStatusResponse>;

public sealed class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusResponse>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;

    public GetSubscriptionStatusQueryHandler(
        ICurrentUserContext currentUser,
        IOrgSubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        _currentUser = currentUser;
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<SubscriptionStatusResponse> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var orgId = _currentUser.OrgId.Value;

        var activeUsers = await _userRepository.CountByOrgIdAsync(orgId, cancellationToken);
        var activeDevices = await _deviceRepository.CountActiveByOrgIdAsync(orgId, cancellationToken);

        var subscription = await _subscriptionRepository.GetByOrgIdAsync(orgId, cancellationToken);

        if (subscription is null || subscription.Plan is null)
        {
            return new SubscriptionStatusResponse
            {
                Status = "none",
                CurrentUsers = activeUsers,
                CurrentDevices = activeDevices
            };
        }

        var plan = subscription.Plan;

        return new SubscriptionStatusResponse
        {
            Status = subscription.Status.ToString().ToLowerInvariant(),
            PlanName = plan.Name,
            PlanTier = plan.Tier.ToString().ToLowerInvariant(),
            Features = new PlanFeatureSet
            {
                Flags = plan.ToFeatureDictionary()
            },
            CurrentPeriodStart = subscription.CurrentPeriodStart,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            TrialEnd = subscription.TrialEnd,
            IsInGracePeriod = subscription.IsInGracePeriod(),
            GracePeriodEnd = subscription.GracePeriodEnd,
            Quantity = subscription.Quantity,
            CurrentUsers = activeUsers,
            CurrentDevices = activeDevices,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CanceledAt = subscription.CanceledAt
        };
    }
}
