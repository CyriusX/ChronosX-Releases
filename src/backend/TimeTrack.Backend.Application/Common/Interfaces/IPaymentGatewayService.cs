namespace TimeTrack.Backend.Application.Common.Interfaces;

public interface IPaymentGatewayService
{
    Task<string> CreateCheckoutSessionAsync(Guid orgId, string stripePriceId, string successUrl, string cancelUrl, int quantity = 1, CancellationToken ct = default);
    Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string stripeSubscriptionId, bool cancelAtPeriodEnd = true, CancellationToken ct = default);
    Task UpdateSubscriptionQuantityAsync(string stripeSubscriptionId, int newQuantity, bool refundFullDifference = false, CancellationToken ct = default);
    Task HandleWebhookEventAsync(string jsonPayload, string signatureHeader, CancellationToken ct = default);
    Task<int> RefundAllPaidChargesAsync(string stripeCustomerId, CancellationToken ct = default);
}
