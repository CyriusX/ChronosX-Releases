using MediatR;
using Microsoft.Extensions.Configuration;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Commands;

public sealed record UpdateSeatsCommand(int NewQuantity) : IRequest<UpdateSeatsResult>;

public sealed record UpdateSeatsResult(int Quantity, string ProrationType);

public sealed class UpdateSeatsCommandHandler : IRequestHandler<UpdateSeatsCommand, UpdateSeatsResult>
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public UpdateSeatsCommandHandler(
        IPaymentGatewayService paymentGateway,
        ICurrentUserContext currentUser,
        IOrgSubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        _paymentGateway = paymentGateway;
        _currentUser = currentUser;
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<UpdateSeatsResult> Handle(UpdateSeatsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var orgId = _currentUser.OrgId.Value;
        var subscription = await _subscriptionRepository.GetByOrgIdAsync(orgId, cancellationToken)
            ?? throw new NotFoundException("OrgSubscription", orgId);

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new ConflictException("no_subscription", "No active Stripe subscription found");

        if (request.NewQuantity < 1)
            throw new ValidationException("newQuantity", "Quantity must be at least 1");

        if (request.NewQuantity == subscription.Quantity)
            throw new ValidationException("newQuantity", "New quantity is the same as current");

        var activeUsers = await _userRepository.CountByOrgIdAsync(orgId, cancellationToken);
        if (request.NewQuantity < activeUsers)
            throw new ValidationException("newQuantity", $"Cannot reduce below {activeUsers} active users");

        var immediateCancelDays = _configuration.GetValue<int>("Stripe:ImmediateCancelDays", 7);
        var isWithinCoolingOff = subscription.CreatedAt.AddDays(immediateCancelDays) > DateTime.UtcNow;
        var isReduction = request.NewQuantity < subscription.Quantity;
        var isAddition = request.NewQuantity > subscription.Quantity;

        var refundFull = isReduction && isWithinCoolingOff;

        await _paymentGateway.UpdateSubscriptionQuantityAsync(
            subscription.StripeSubscriptionId,
            request.NewQuantity,
            refundFullDifference: refundFull,
            cancellationToken);

        subscription.UpdateQuantity(request.NewQuantity);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        var prorationType = refundFull ? "full_refund" : isAddition ? "proration_charge" : "proration_credit";

        return new UpdateSeatsResult(request.NewQuantity, prorationType);
    }
}
