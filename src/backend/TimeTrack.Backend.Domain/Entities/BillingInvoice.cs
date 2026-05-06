namespace TimeTrack.Backend.Domain.Entities;

public sealed class BillingInvoice
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string StripeInvoiceId { get; private set; } = string.Empty;
    public long AmountCents { get; private set; }
    public string Currency { get; private set; } = "usd";
    public string Status { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? PlanName { get; private set; }
    public int Quantity { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public string? PdfUrl { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? RefundStatus { get; private set; }
    public long? RefundAmountCents { get; private set; }
    public string? RefundId { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    private BillingInvoice() { }

    public static BillingInvoice Create(
        Guid orgId,
        string stripeInvoiceId,
        long amountCents,
        string currency,
        string status,
        DateTime periodStart,
        DateTime periodEnd,
        int quantity = 1,
        string? description = null,
        string? planName = null,
        string? pdfUrl = null,
        DateTime? paidAt = null)
    {
        return new BillingInvoice
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            StripeInvoiceId = stripeInvoiceId,
            AmountCents = amountCents,
            Currency = currency,
            Status = status,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Quantity = quantity,
            Description = description,
            PlanName = planName,
            PdfUrl = pdfUrl,
            PaidAt = paidAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateFromStripe(
        long amountCents,
        string status,
        string? pdfUrl,
        DateTime? paidAt)
    {
        AmountCents = amountCents;
        Status = status;
        PdfUrl = pdfUrl;
        PaidAt = paidAt;
    }

    public void MarkCancelled()
    {
        Status = "cancelled";
    }

    public void MarkRefunded(long amountCents, string refundId)
    {
        RefundStatus = "refunded";
        RefundAmountCents = amountCents;
        RefundId = refundId;
        RefundedAt = DateTime.UtcNow;
    }
}
