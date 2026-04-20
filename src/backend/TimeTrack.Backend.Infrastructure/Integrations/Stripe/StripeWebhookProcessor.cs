using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Infrastructure.Integrations.Stripe;

public sealed class StripeWebhookProcessor
{
    private readonly IOrgSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IStripeEventLogRepository _eventLogRepository;
    private readonly IBillingInvoiceRepository _invoiceRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookProcessor> _logger;

    public StripeWebhookProcessor(
        IOrgSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IStripeEventLogRepository eventLogRepository,
        IBillingInvoiceRepository invoiceRepository,
        IConfiguration configuration,
        ILogger<StripeWebhookProcessor> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _eventLogRepository = eventLogRepository;
        _invoiceRepository = invoiceRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessAsync(global::Stripe.Event stripeEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing Stripe event {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        if (await _eventLogRepository.HasBeenProcessedAsync(stripeEvent.Id, ct))
        {
            _logger.LogInformation("Stripe event {EventId} already processed, skipping", stripeEvent.Id);
            return;
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case global::Stripe.EventTypes.CheckoutSessionCompleted:
                    await HandleCheckoutSessionCompleted(stripeEvent, ct);
                    break;

                case global::Stripe.EventTypes.CustomerSubscriptionUpdated:
                    await HandleSubscriptionUpdated(stripeEvent, ct);
                    break;

                case global::Stripe.EventTypes.CustomerSubscriptionDeleted:
                    await HandleSubscriptionDeleted(stripeEvent, ct);
                    break;

                case global::Stripe.EventTypes.InvoicePaymentSucceeded:
                    await HandlePaymentSucceeded(stripeEvent, ct);
                    break;

                case global::Stripe.EventTypes.InvoicePaymentFailed:
                    await HandlePaymentFailed(stripeEvent, ct);
                    break;

                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            await LogEventAsync(stripeEvent, "processed", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Stripe event {EventId}", stripeEvent.Id);
            await LogEventAsync(stripeEvent, "failed", ct, ex.Message);
        }
    }

    private async Task HandleCheckoutSessionCompleted(global::Stripe.Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        if (session is null) return;

        var orgIdStr = session.Metadata?.GetValueOrDefault("orgId");
        if (string.IsNullOrEmpty(orgIdStr) || !Guid.TryParse(orgIdStr, out var orgId))
        {
            _logger.LogWarning("Checkout session {SessionId} has no orgId in metadata", session.Id);
            return;
        }

        var subscription = await _subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, ct);
        if (subscription is null)
        {
            _logger.LogInformation("Creating new OrgSubscription for org {OrgId} from checkout", orgId);
            subscription = OrgSubscription.Create(orgId);
            try
            {
                await _subscriptionRepository.AddAsync(subscription, ct);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                _logger.LogInformation("OrgSubscription for org {OrgId} already exists (race condition), fetching existing", orgId);
                subscription = await _subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, ct);
                if (subscription is null) return;
            }
        }

        var subId = session.SubscriptionId;
        if (string.IsNullOrEmpty(subId)) return;

        var stripeSubscriptionService = new global::Stripe.SubscriptionService();
        var stripeSubscription = await stripeSubscriptionService.GetAsync(subId, cancellationToken: ct);

        var firstItem = stripeSubscription.Items.Data.FirstOrDefault();
        var priceId = firstItem?.Price.Id;
        var quantity = (int)(firstItem?.Quantity ?? 1L);

        var planIdStr = stripeSubscription.Metadata?.GetValueOrDefault("planId");
        Guid? planId = null;
        if (!string.IsNullOrEmpty(planIdStr) && Guid.TryParse(planIdStr, out var pid))
            planId = pid;

        if (planId is null && !string.IsNullOrEmpty(priceId))
        {
            var plan = await _planRepository.GetByStripePriceIdAsync(priceId, ct);
            planId = plan?.Id;
        }

        var periodStart = firstItem?.CurrentPeriodStart ?? stripeSubscription.Created;
        var periodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        subscription.Activate(
            planId ?? Guid.Empty,
            stripeSubscription.Id,
            stripeSubscription.CustomerId,
            periodStart,
            periodEnd,
            (int)quantity);

        await _subscriptionRepository.UpdateAsync(subscription, ct);
    }

    private async Task HandleSubscriptionUpdated(global::Stripe.Event stripeEvent, CancellationToken ct)
    {
        var stripeSub = stripeEvent.Data.Object as global::Stripe.Subscription;
        if (stripeSub is null) return;

        var subscription = await _subscriptionRepository.GetByStripeSubscriptionIdAsync(stripeSub.Id, ct);
        if (subscription is null)
        {
            _logger.LogWarning("No OrgSubscription found for Stripe subscription {SubId}", stripeSub.Id);
            return;
        }

        var firstItem = stripeSub.Items.Data.FirstOrDefault();
        if (firstItem is not null)
        {
            subscription.UpdatePeriod(firstItem.CurrentPeriodStart, firstItem.CurrentPeriodEnd);
            subscription.UpdateQuantity((int)firstItem.Quantity);

            // Detect plan change via price_id
            var priceId = firstItem.Price.Id;
            if (!string.IsNullOrEmpty(priceId))
            {
                var newPlan = await _planRepository.GetByStripePriceIdAsync(priceId, ct);
                if (newPlan is not null && newPlan.Id != subscription.PlanId)
                {
                    _logger.LogInformation("Plan change detected: {OldPlan} -> {NewPlan} for org {OrgId}",
                        subscription.PlanId, newPlan.Id, subscription.OrgId);
                    subscription.ChangePlan(newPlan.Id);
                }
            }
        }

        // Handle cancel_at_period_end from Stripe
        if (stripeSub.CancelAtPeriodEnd && !subscription.CancelAtPeriodEnd)
        {
            subscription.Cancel(atPeriodEnd: true);
        }
        else if (!stripeSub.CancelAtPeriodEnd && subscription.CancelAtPeriodEnd && stripeSub.Status == "active")
        {
            // Subscription was reactivated (cancel reversed)
            subscription.Reactivate(
                subscription.PlanId ?? Guid.Empty,
                subscription.CurrentPeriodStart ?? DateTime.UtcNow,
                subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1),
                subscription.Quantity);
        }

        var newStatus = stripeSub.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.Unpaid,
            "incomplete" => SubscriptionStatus.Incomplete,
            _ => subscription.Status
        };

        if (newStatus == SubscriptionStatus.PastDue && subscription.Status != SubscriptionStatus.PastDue)
        {
            var gracePeriodDays = _configuration.GetValue<int>("Stripe:GracePeriodDays", 7);
            subscription.MarkPastDue(gracePeriodDays);
        }
        else if (newStatus != subscription.Status)
        {
            subscription.SetStatus(newStatus);
        }

        await _subscriptionRepository.UpdateAsync(subscription, ct);
    }

    private async Task HandleSubscriptionDeleted(global::Stripe.Event stripeEvent, CancellationToken ct)
    {
        var stripeSub = stripeEvent.Data.Object as global::Stripe.Subscription;
        if (stripeSub is null) return;

        var subscription = await _subscriptionRepository.GetByStripeSubscriptionIdAsync(stripeSub.Id, ct);
        if (subscription is null) return;

        subscription.Cancel(atPeriodEnd: false);
        await _subscriptionRepository.UpdateAsync(subscription, ct);

        await _invoiceRepository.CancelByOrgIdAsync(subscription.OrgId, ct);
        _logger.LogInformation("Cancelled all invoices for org {OrgId} after subscription deletion", subscription.OrgId);
    }

    private async Task HandlePaymentSucceeded(global::Stripe.Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as global::Stripe.Invoice;
        if (invoice is null) return;

        var customerId = invoice.CustomerId;
        if (string.IsNullOrEmpty(customerId)) return;

        OrgSubscription? subscription = null;
        for (var i = 0; i < 3; i++)
        {
            subscription = await _subscriptionRepository.GetByStripeCustomerIdAsync(customerId, ct);
            if (subscription is not null) break;
            _logger.LogWarning("Invoice {InvoiceId}: subscription not found for customer {CustomerId}, retry {Attempt}", invoice.Id, customerId, i + 1);
            await Task.Delay(500, ct);
        }

        if (subscription is null)
        {
            _logger.LogError("Invoice {InvoiceId}: could not find subscription for customer {CustomerId} after retries", invoice.Id, customerId);
            return;
        }

        // Reactivate if past due / unpaid
        if (subscription.Status == SubscriptionStatus.PastDue || subscription.Status == SubscriptionStatus.Unpaid)
        {
            subscription.Reactivate(
                subscription.PlanId ?? Guid.Empty,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                subscription.Quantity);

            await _subscriptionRepository.UpdateAsync(subscription, ct);
        }

        // Persist invoice
        await PersistInvoiceAsync(invoice, subscription, ct);
    }

    private async Task PersistInvoiceAsync(global::Stripe.Invoice stripeInvoice, OrgSubscription subscription, CancellationToken ct)
    {
        var existing = await _invoiceRepository.GetByStripeInvoiceIdAsync(stripeInvoice.Id, ct);
        var paidAt = stripeInvoice.StatusTransitions?.PaidAt;

        var lineItem = stripeInvoice.Lines?.Data?.FirstOrDefault();
        var lineAmount = lineItem?.Amount ?? 0;

        var amountCents = stripeInvoice.Total > 0 ? stripeInvoice.Total
            : stripeInvoice.AmountPaid > 0 ? stripeInvoice.AmountPaid
            : stripeInvoice.Subtotal > 0 ? stripeInvoice.Subtotal
            : lineAmount;

        if (existing is not null)
        {
            if (existing.AmountCents > 0 && amountCents == 0)
            {
                _logger.LogInformation("Invoice {InvoiceId} already has amount {ExistingAmount}, skipping webhook update with 0",
                    stripeInvoice.Id, existing.AmountCents);

                if (!string.IsNullOrEmpty(stripeInvoice.InvoicePdf) && string.IsNullOrEmpty(existing.PdfUrl))
                {
                    existing.UpdateFromStripe(existing.AmountCents, stripeInvoice.Status ?? "paid", stripeInvoice.InvoicePdf, paidAt);
                    await _invoiceRepository.UpdateAsync(existing, ct);
                }
                return;
            }

            existing.UpdateFromStripe(
                amountCents,
                stripeInvoice.Status ?? "paid",
                stripeInvoice.InvoicePdf,
                stripeInvoice.Status == "paid" ? paidAt : null);
            await _invoiceRepository.UpdateAsync(existing, ct);
            return;
        }

        var planName = subscription.Plan?.Name ?? "ChronosX Pro";
        var lines = stripeInvoice.Lines?.Data ?? [];

        // Proration invoices have both credit (negative) and charge (positive) lines
        var chargeLine = lines.FirstOrDefault(l => l.Amount > 0) ?? lineItem;
        var creditLine = lines.FirstOrDefault(l => l.Amount < 0);
        var isProration = creditLine is not null && lines.Count >= 2;

        int quantity;
        string description;

        if (isProration)
        {
            // Proration: calculate added/removed seats from line quantities
            var newQty = (int)(chargeLine?.Quantity ?? subscription.Quantity);
            var oldQty = (int)(creditLine?.Quantity ?? subscription.Quantity);
            var delta = newQty - oldQty;

            if (delta > 0)
            {
                quantity = delta;
                description = $"{delta} × {planName} adicional — prorata";
            }
            else if (delta < 0)
            {
                quantity = Math.Abs(delta);
                description = $"Redução de {Math.Abs(delta)} × {planName} — crédito proporcional";
            }
            else
            {
                quantity = subscription.Quantity;
                description = $"Ajuste {planName}";
            }
        }
        else
        {
            quantity = (int)(chargeLine?.Quantity ?? subscription.Quantity);
            var pricePerSeat = amountCents > 0 && quantity > 0 ? (decimal)amountCents / quantity : 0;
            description = $"{quantity} × {planName} (a ${pricePerSeat / 100:F2}/mês)";
        }

        var billingInvoice = BillingInvoice.Create(
            orgId: subscription.OrgId,
            stripeInvoiceId: stripeInvoice.Id,
            amountCents: amountCents,
            currency: stripeInvoice.Currency ?? "usd",
            status: stripeInvoice.Status ?? "paid",
            periodStart: stripeInvoice.PeriodStart,
            periodEnd: stripeInvoice.PeriodEnd,
            quantity: quantity,
            description: description,
            planName: planName,
            pdfUrl: stripeInvoice.InvoicePdf,
            paidAt: stripeInvoice.Status == "paid" ? paidAt : null);

        try
        {
            await _invoiceRepository.AddAsync(billingInvoice, ct);
            _logger.LogInformation("Persisted billing invoice {InvoiceId} for org {OrgId}, amount: {Amount} (Total={Total}, AmountPaid={AmountPaid}, Subtotal={Subtotal}, LineAmount={LineAmount})",
                stripeInvoice.Id, subscription.OrgId, amountCents, stripeInvoice.Total, stripeInvoice.AmountPaid, stripeInvoice.Subtotal, lineAmount);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            _logger.LogWarning("Invoice {InvoiceId} already persisted (duplicate)", stripeInvoice.Id);
        }
    }

    private async Task HandlePaymentFailed(global::Stripe.Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as global::Stripe.Invoice;
        if (invoice is null) return;

        var customerId = invoice.CustomerId;
        if (string.IsNullOrEmpty(customerId)) return;

        var subscription = await _subscriptionRepository.GetByStripeCustomerIdAsync(customerId, ct);
        if (subscription is null) return;

        if (subscription.Status != SubscriptionStatus.PastDue)
        {
            var gracePeriodDays = _configuration.GetValue<int>("Stripe:GracePeriodDays", 7);
            subscription.MarkPastDue(gracePeriodDays);
            await _subscriptionRepository.UpdateAsync(subscription, ct);
        }
    }

    private async Task LogEventAsync(global::Stripe.Event stripeEvent, string status, CancellationToken ct, string? errorMessage = null)
    {
        Guid orgId = Guid.Empty;
        try
        {
            var sub = stripeEvent.Data.Object as global::Stripe.Subscription;
            var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
            var invoice = stripeEvent.Data.Object as global::Stripe.Invoice;

            var orgIdStr = sub?.Metadata?.GetValueOrDefault("orgId")
                ?? session?.Metadata?.GetValueOrDefault("orgId");

            if (!string.IsNullOrEmpty(orgIdStr))
                Guid.TryParse(orgIdStr, out orgId);

            if (orgId == Guid.Empty)
            {
                var stripeId = sub?.Id;
                if (!string.IsNullOrEmpty(stripeId))
                {
                    var found = await _subscriptionRepository.GetByStripeSubscriptionIdAsync(stripeId, ct);
                    if (found is not null)
                        orgId = found.OrgId;
                }
            }

            if (orgId == Guid.Empty)
            {
                var customerId = invoice?.CustomerId;
                if (!string.IsNullOrEmpty(customerId))
                {
                    var found = await _subscriptionRepository.GetByStripeCustomerIdAsync(customerId, ct);
                    if (found is not null)
                        orgId = found.OrgId;
                }
            }
        }
 catch { }

        var eventLog = StripeEventLog.Create(
            orgId,
            stripeEvent.Id,
            stripeEvent.Type,
            status,
            errorMessage: errorMessage);

        try
        {
            await _eventLogRepository.AddAsync(eventLog, ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Duplicate key — event already logged, ignore
        }
    }
}
