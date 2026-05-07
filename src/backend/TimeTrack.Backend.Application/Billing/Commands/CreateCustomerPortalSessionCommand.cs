using MediatR;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Commands;

public sealed record CreateCustomerPortalSessionCommand(
    string ReturnUrl) : IRequest<CustomerPortalResponse>;

public sealed class CreateCustomerPortalSessionCommandHandler : IRequestHandler<CreateCustomerPortalSessionCommand, CustomerPortalResponse>
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOrgSubscriptionRepository _subscriptionRepository;

    public CreateCustomerPortalSessionCommandHandler(
        IPaymentGatewayService paymentGateway,
        ICurrentUserContext currentUser,
        IOrgSubscriptionRepository subscriptionRepository)
    {
        _paymentGateway = paymentGateway;
        _currentUser = currentUser;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<CustomerPortalResponse> Handle(CreateCustomerPortalSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var subscription = await _subscriptionRepository.GetByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken)
            ?? throw new NotFoundException("OrgSubscription", _currentUser.OrgId.Value);

        if (string.IsNullOrEmpty(subscription.StripeCustomerId))
            throw new ConflictException("no_customer", "No Stripe customer found for this organization");

        var portalUrl = await _paymentGateway.CreateCustomerPortalSessionAsync(
            subscription.StripeCustomerId,
            request.ReturnUrl,
            cancellationToken);

        return new CustomerPortalResponse { PortalUrl = portalUrl };
    }
}
