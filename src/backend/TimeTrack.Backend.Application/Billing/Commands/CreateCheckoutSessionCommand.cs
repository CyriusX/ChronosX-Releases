using MediatR;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Commands;

public sealed record CreateCheckoutSessionCommand(
    Guid PlanId,
    string SuccessUrl,
    string CancelUrl,
    int Quantity = 1) : IRequest<CheckoutSessionResponse>;

public sealed class CreateCheckoutSessionCommandHandler : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSessionResponse>
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISubscriptionPlanRepository _planRepository;

    public CreateCheckoutSessionCommandHandler(
        IPaymentGatewayService paymentGateway,
        ICurrentUserContext currentUser,
        ISubscriptionPlanRepository planRepository)
    {
        _paymentGateway = paymentGateway;
        _currentUser = currentUser;
        _planRepository = planRepository;
    }

    public async Task<CheckoutSessionResponse> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var plan = await _planRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException("SubscriptionPlan", request.PlanId);

        var checkoutUrl = await _paymentGateway.CreateCheckoutSessionAsync(
            _currentUser.OrgId.Value,
            plan.StripePriceId,
            request.SuccessUrl,
            request.CancelUrl,
            request.Quantity,
            cancellationToken);

        return new CheckoutSessionResponse { CheckoutUrl = checkoutUrl, SessionId = "" };
    }
}
