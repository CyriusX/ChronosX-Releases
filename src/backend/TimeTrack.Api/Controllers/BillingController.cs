using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Billing.Commands;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Billing.Queries;
using TimeTrack.Backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
public sealed class BillingController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BillingController> _logger;

    public BillingController(ISender mediator, IPaymentGatewayService paymentGateway, IConfiguration configuration, ILogger<BillingController> logger)
    {
        _mediator = mediator;
        _paymentGateway = paymentGateway;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> GetPlans(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAvailablePlansQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subscription")]
    [ProducesResponseType(typeof(SubscriptionStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionStatusResponse>> GetSubscriptionStatus(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubscriptionStatusQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("checkout")]
    [RequireAdmin]
    [ProducesResponseType(typeof(CheckoutSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CheckoutSessionResponse>> CreateCheckoutSession(
        [FromBody] CreateCheckoutRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["Frontend:BaseUrl"] ?? "https://app.timetrack.com";
        var successUrl = string.IsNullOrWhiteSpace(request.SuccessUrl) ? $"{baseUrl}/settings?billing=success" : request.SuccessUrl;
        var cancelUrl = string.IsNullOrWhiteSpace(request.CancelUrl) ? $"{baseUrl}/settings?billing=canceled" : request.CancelUrl;

        // Stripe live mode requires HTTPS for all URLs
        successUrl = EnsureHttps(successUrl);
        cancelUrl = EnsureHttps(cancelUrl);

        try
        {
            _logger.LogInformation("Creating checkout session: PlanId={PlanId}, SuccessUrl={SuccessUrl}", request.PlanId, successUrl);
            var result = await _mediator.Send(new CreateCheckoutSessionCommand(
                request.PlanId,
                successUrl,
                cancelUrl,
                request.Quantity), cancellationToken);
            return Ok(result);
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe error on checkout: {Message} (Code={Code}, StripeCode={StripeCode})",
                ex.Message, ex.StripeError?.Code, ex.StripeError?.DeclineCode);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "stripe_error",
                message = ex.StripeError?.Message ?? ex.Message,
                stripeCode = ex.StripeError?.Code
            });
        }
    }

    private static string EnsureHttps(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url["http://".Length..];
        return url;
    }

    [HttpPost("portal")]
    [RequireAdmin]
    [ProducesResponseType(typeof(CustomerPortalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerPortalResponse>> CreateCustomerPortalSession(
        [FromBody] CreatePortalRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["Frontend:BaseUrl"] ?? "https://app.timetrack.com";
        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl) ? $"{baseUrl}/settings" : request.ReturnUrl;

        var result = await _mediator.Send(new CreateCustomerPortalSessionCommand(
            returnUrl), cancellationToken);
        return Ok(result);
    }

    [HttpPost("webhooks/stripe")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        var jsonPayload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(cancellationToken);
        var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        await _paymentGateway.HandleWebhookEventAsync(jsonPayload, signatureHeader, cancellationToken);
        return Ok();
    }

    [HttpPost("cancel")]
    [RequireAdmin]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelSubscriptionCommand(), cancellationToken);
        return Ok(new
        {
            immediateCancel = result.ImmediateCancel,
            effectiveDate = result.EffectiveDate,
            refundedInvoices = result.RefundedInvoices,
            message = result.ImmediateCancel
                ? $"Subscription cancelled immediately. {result.RefundedInvoices} charge(s) refunded."
                : "Subscription cancellation scheduled at period end"
        });
    }

    [HttpGet("subscription/features")]
    [ProducesResponseType(typeof(PlanFeatureSet), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanFeatureSet>> GetSubscriptionFeatures(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubscriptionStatusQuery(), cancellationToken);
        return Ok(result.Features);
    }

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> GetInvoices(
        [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetInvoicesQuery(limit), cancellationToken);
        return Ok(result);
    }

    [HttpPut("seats")]
    [RequireAdmin]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSeats(
        [FromBody] UpdateSeatsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateSeatsCommand(request.NewQuantity), cancellationToken);
        return Ok(new
        {
            quantity = result.Quantity,
            prorationType = result.ProrationType
        });
    }
}

public record CreateCheckoutRequest(Guid PlanId, int Quantity = 1, string? SuccessUrl = null, string? CancelUrl = null);
public record CreatePortalRequest(string? ReturnUrl = null);
public record UpdateSeatsRequest(int NewQuantity);
