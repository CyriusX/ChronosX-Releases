using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Integrations.Stripe;

public sealed class StripeService : IPaymentGatewayService
{
    private readonly global::Stripe.Checkout.SessionService _checkoutSessionService;
    private readonly global::Stripe.BillingPortal.SessionService _portalSessionService;
    private readonly global::Stripe.SubscriptionService _subscriptionService;
    private readonly global::Stripe.BillingPortal.ConfigurationService _portalConfigService;
    private readonly global::Stripe.ProductService _productService;
    private readonly global::Stripe.PriceService _priceService;
    private readonly StripeWebhookProcessor _webhookProcessor;
    private readonly ILogger<StripeService> _logger;
    private readonly string _webhookSecret;
    private readonly bool _isConfigured;
    private string? _cachedPortalConfigId;

    public StripeService(
        IConfiguration configuration,
        StripeWebhookProcessor webhookProcessor,
        ILogger<StripeService> logger)
    {
        var secretKey = configuration["Stripe:SecretKey"] ?? "";
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? "";
        _isConfigured = !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(_webhookSecret);

        if (_isConfigured)
        {
            global::Stripe.StripeConfiguration.ApiKey = secretKey;
        }

        _checkoutSessionService = new();
        _portalSessionService = new();
        _subscriptionService = new();
        _portalConfigService = new();
        _productService = new();
        _priceService = new();
        _webhookProcessor = webhookProcessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StripePlanInfo>> ListPlansAsync(CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured. Set Stripe:SecretKey and Stripe:WebhookSecret.");

        var products = await _productService.ListAsync(new global::Stripe.ProductListOptions
        {
            Active = true,
        }, cancellationToken: ct);

        var result = new List<StripePlanInfo>();

        foreach (var product in products)
        {
            if (product.DefaultPriceId is null) continue;

            global::Stripe.Price? price = null;
            try
            {
                price = await _priceService.GetAsync(product.DefaultPriceId, cancellationToken: ct);
            }
            catch (global::Stripe.StripeException ex)
            {
                _logger.LogWarning(ex, "Skipping product {ProductId}: default price {PriceId} not found",
                    product.Id, product.DefaultPriceId);
                continue;
            }

            if (price?.Recurring is null) continue;

            var tier = product.Metadata.TryGetValue("tier", out var tierValue) ? tierValue : product.Name.ToLowerInvariant();
            var maxUsers = product.Metadata.TryGetValue("max_users", out var maxUsersStr) && int.TryParse(maxUsersStr, out var mu) ? mu : 0;
            var maxDevices = product.Metadata.TryGetValue("max_devices", out var maxDevicesStr) && int.TryParse(maxDevicesStr, out var md) ? md : 0;

            var features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (product.Metadata.TryGetValue("features", out var featuresStr))
            {
                foreach (var f in featuresStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    features[f] = true;
            }

            result.Add(new StripePlanInfo
            {
                StripeProductId = product.Id,
                StripePriceId = price.Id,
                Name = product.Name,
                Description = product.Description,
                Tier = tier,
                MonthlyPriceCents = (int)(price.UnitAmount ?? 0),
                MaxUsers = maxUsers,
                MaxDevices = maxDevices,
                Features = features
            });
        }

        return result;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Guid orgId, string stripePriceId, string successUrl, string cancelUrl, int quantity = 1, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured. Set Stripe:SecretKey and Stripe:WebhookSecret.");

        var options = new global::Stripe.Checkout.SessionCreateOptions
        {
            Mode = "subscription",
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new global::Stripe.Checkout.SessionLineItemOptions
                {
                    Price = stripePriceId,
                    Quantity = quantity
                }
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["orgId"] = orgId.ToString()
            },
            SubscriptionData = new global::Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["orgId"] = orgId.ToString()
                }
            }
        };

        try
        {
            var session = await _checkoutSessionService.CreateAsync(options, cancellationToken: ct);
            return session.Url;
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed. OrgId={OrgId}, PriceId={PriceId}, StripeError={StripeError}",
                orgId, stripePriceId, ex.StripeError?.Message ?? ex.Message);
            throw;
        }
    }

    public async Task<string> CreateCustomerPortalSessionAsync(
        string stripeCustomerId, string returnUrl, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured. Set Stripe:SecretKey and Stripe:WebhookSecret.");

        var configId = await GetOrCreatePortalConfigurationAsync(ct);

        var options = new global::Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl,
            Configuration = configId
        };

        var session = await _portalSessionService.CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId, bool cancelAtPeriodEnd = true, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        if (cancelAtPeriodEnd)
        {
            var options = new global::Stripe.SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            };
            await _subscriptionService.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
        }
        else
        {
            await _subscriptionService.CancelAsync(stripeSubscriptionId, cancellationToken: ct);
        }
    }

    public async Task UpdateSubscriptionQuantityAsync(string stripeSubscriptionId, int newQuantity, bool refundFullDifference = false, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        var subscription = await _subscriptionService.GetAsync(stripeSubscriptionId, cancellationToken: ct);
        var firstItem = subscription.Items.Data.FirstOrDefault();
        var oldQty = (int)(firstItem?.Quantity ?? 1);

        _logger.LogInformation("Seat update: {OldQty} -> {NewQty} on subscription {SubId}",
            oldQty, newQuantity, stripeSubscriptionId);

        // Full refund path: update without proration, then issue refund manually
        if (refundFullDifference && newQuantity < oldQty)
        {
            var options = new global::Stripe.SubscriptionUpdateOptions
            {
                Items =
                [
                    new global::Stripe.SubscriptionItemOptions
                    {
                        Id = firstItem?.Id,
                        Quantity = newQuantity
                    }
                ],
                ProrationBehavior = "none"
            };

            await _subscriptionService.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);

            if (string.IsNullOrEmpty(subscription.CustomerId)) return;

            var priceId = firstItem?.Price?.Id;
            var priceService = new global::Stripe.PriceService();
            var price = !string.IsNullOrEmpty(priceId) ? await priceService.GetAsync(priceId, cancellationToken: ct) : null;
            var pricePerSeat = price?.UnitAmount ?? 0;
            var refundAmount = pricePerSeat * (oldQty - newQuantity);

            if (refundAmount > 0)
            {
                var chargeService = new global::Stripe.ChargeService();
                var charges = await chargeService.ListAsync(new global::Stripe.ChargeListOptions
                {
                    Customer = subscription.CustomerId,
                    Limit = 1
                }, cancellationToken: ct);

                var latestCharge = charges.FirstOrDefault();
                if (latestCharge is not null)
                {
                    var refundService = new global::Stripe.RefundService();
                    await refundService.CreateAsync(new global::Stripe.RefundCreateOptions
                    {
                        Charge = latestCharge.Id,
                        Amount = refundAmount,
                        Reason = "requested_by_customer"
                    }, cancellationToken: ct);

                    _logger.LogInformation("Issued refund of {Amount} cents for seat reduction ({OldQty} -> {NewQty}) on subscription {SubId}",
                        refundAmount, oldQty, newQuantity, stripeSubscriptionId);
                }
            }

            return;
        }

        // Standard path: let Stripe handle proration and invoice immediately
        var updateOptions = new global::Stripe.SubscriptionUpdateOptions
        {
            Items =
            [
                new global::Stripe.SubscriptionItemOptions
                {
                    Id = firstItem?.Id,
                    Quantity = newQuantity
                }
            ],
            ProrationBehavior = "always_invoice"
        };

        await _subscriptionService.UpdateAsync(stripeSubscriptionId, updateOptions, cancellationToken: ct);

        _logger.LogInformation("Updated subscription {SubId} quantity {OldQty} -> {NewQty} with Stripe proration (always_invoice)",
            stripeSubscriptionId, oldQty, newQuantity);
    }

    private async Task<string> GetOrCreatePortalConfigurationAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedPortalConfigId))
            return _cachedPortalConfigId;

        var configs = await _portalConfigService.ListAsync(new global::Stripe.BillingPortal.ConfigurationListOptions
        {
            Limit = 100,
            Active = true
        }, cancellationToken: ct);

        var existing = configs.FirstOrDefault(c => c.BusinessProfile?.Headline != null);
        if (existing is not null)
        {
            _cachedPortalConfigId = existing.Id;
            return _cachedPortalConfigId;
        }

        var createOptions = new global::Stripe.BillingPortal.ConfigurationCreateOptions
        {
            BusinessProfile = new global::Stripe.BillingPortal.ConfigurationBusinessProfileOptions
            {
                Headline = "ChronosX - Gerencie sua assinatura"
            },
            Features = new global::Stripe.BillingPortal.ConfigurationFeaturesOptions
            {
                SubscriptionUpdate = new global::Stripe.BillingPortal.ConfigurationFeaturesSubscriptionUpdateOptions
                {
                    Enabled = true,
                    DefaultAllowedUpdates = ["plan", "price", "promotion_code"],
                    ProrationBehavior = "create_prorations"
                },
                SubscriptionCancel = new global::Stripe.BillingPortal.ConfigurationFeaturesSubscriptionCancelOptions
                {
                    Enabled = true,
                    Mode = "at_period_end",
                    CancellationReason = new global::Stripe.BillingPortal.ConfigurationFeaturesSubscriptionCancelCancellationReasonOptions
                    {
                        Enabled = true,
                        Options = ["too_expensive", "missing_features", "switched_service", "unused", "other"]
                    }
                },
                InvoiceHistory = new global::Stripe.BillingPortal.ConfigurationFeaturesInvoiceHistoryOptions
                {
                    Enabled = true
                },
                PaymentMethodUpdate = new global::Stripe.BillingPortal.ConfigurationFeaturesPaymentMethodUpdateOptions
                {
                    Enabled = true
                }
            }
        };

        var config = await _portalConfigService.CreateAsync(createOptions, cancellationToken: ct);
        _cachedPortalConfigId = config.Id;
        _logger.LogInformation("Created Stripe portal configuration {ConfigId}", config.Id);
        return _cachedPortalConfigId;
    }

    public async Task HandleWebhookEventAsync(string jsonPayload, string signatureHeader, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        var stripeEvent = global::Stripe.EventUtility.ConstructEvent(
            jsonPayload,
            signatureHeader,
            _webhookSecret,
            throwOnApiVersionMismatch: false);

        await _webhookProcessor.ProcessAsync(stripeEvent, ct);
    }

    public async Task<int> RefundAllPaidChargesAsync(string stripeCustomerId, CancellationToken ct = default)
    {
        if (!_isConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        var chargeService = new global::Stripe.ChargeService();
        var charges = await chargeService.ListAsync(new global::Stripe.ChargeListOptions
        {
            Customer = stripeCustomerId,
            Limit = 100,
        }, cancellationToken: ct);

        var refundService = new global::Stripe.RefundService();
        var refunded = 0;

        foreach (var charge in charges.Where(c => c.Paid && !c.Refunded))
        {
            try
            {
                await refundService.CreateAsync(new global::Stripe.RefundCreateOptions
                {
                    Charge = charge.Id,
                    Reason = "requested_by_customer",
                }, cancellationToken: ct);

                refunded++;
                _logger.LogInformation("Refunded charge {ChargeId} for customer {CustomerId}", charge.Id, stripeCustomerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refund charge {ChargeId} for customer {CustomerId}", charge.Id, stripeCustomerId);
            }
        }

        return refunded;
    }
}
