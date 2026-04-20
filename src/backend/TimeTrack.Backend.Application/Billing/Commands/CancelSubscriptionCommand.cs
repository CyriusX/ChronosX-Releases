using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Commands;

public sealed record CancelSubscriptionCommand : IRequest<CancelSubscriptionResult>;

public sealed record CancelSubscriptionResult(bool ImmediateCancel, DateTime? EffectiveDate, int RefundedInvoices);

public sealed class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, CancelSubscriptionResult>
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly IBillingInvoiceRepository _invoiceRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CancelSubscriptionCommandHandler> _logger;

    public CancelSubscriptionCommandHandler(
        IPaymentGatewayService paymentGateway,
        ICurrentUserContext currentUser,
        IOrgSubscriptionRepository subscriptionRepository,
        IBillingInvoiceRepository invoiceRepository,
        IConfiguration configuration,
        ILogger<CancelSubscriptionCommandHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _currentUser = currentUser;
        _subscriptionRepository = subscriptionRepository;
        _invoiceRepository = invoiceRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CancelSubscriptionResult> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var subscription = await _subscriptionRepository.GetByOrgIdAsync(_currentUser.OrgId.Value, cancellationToken)
            ?? throw new NotFoundException("OrgSubscription", _currentUser.OrgId.Value);

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new ConflictException("no_subscription", "No active Stripe subscription found");

        var immediateCancelDays = _configuration.GetValue<int>("Stripe:ImmediateCancelDays", 7);
        var isWithinCoolingOff = subscription.CreatedAt.AddDays(immediateCancelDays) > DateTime.UtcNow;

        if (isWithinCoolingOff)
        {
            await _paymentGateway.CancelSubscriptionAsync(
                subscription.StripeSubscriptionId,
                cancelAtPeriodEnd: false,
                cancellationToken);

            subscription.Cancel(atPeriodEnd: false);
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

            var refundedInvoices = 0;

            if (!string.IsNullOrEmpty(subscription.StripeCustomerId))
            {
                refundedInvoices = await _paymentGateway.RefundAllPaidChargesAsync(
                    subscription.StripeCustomerId, cancellationToken);

                _logger.LogInformation("Refunded {Count} charges for org {OrgId} during cooling-off cancellation",
                    refundedInvoices, subscription.OrgId);
            }

            await _invoiceRepository.MarkRefundedByOrgIdAsync(subscription.OrgId, cancellationToken);

            return new CancelSubscriptionResult(ImmediateCancel: true, EffectiveDate: null, RefundedInvoices: refundedInvoices);
        }
        else
        {
            await _paymentGateway.CancelSubscriptionAsync(
                subscription.StripeSubscriptionId,
                cancelAtPeriodEnd: true,
                cancellationToken);

            subscription.Cancel(atPeriodEnd: true);
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

            return new CancelSubscriptionResult(ImmediateCancel: false, EffectiveDate: subscription.CurrentPeriodEnd, RefundedInvoices: 0);
        }
    }
}
