namespace TimeTrack.Backend.Application.Billing.DTOs;

public record SubscriptionStatusResponse
{
    public string Status { get; init; } = "none";
    public string? PlanName { get; init; }
    public string? PlanTier { get; init; }
    public PlanFeatureSet? Features { get; init; }
    public DateTime? CurrentPeriodStart { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public DateTime? TrialEnd { get; init; }
    public bool IsInGracePeriod { get; init; }
    public DateTime? GracePeriodEnd { get; init; }
    public int Quantity { get; init; }
    public int CurrentUsers { get; init; }
    public int CurrentDevices { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public DateTime? CanceledAt { get; init; }
}

public record PlanFeatureSet
{
    public Dictionary<string, bool> Flags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public record PlanResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Tier { get; init; } = string.Empty;
    public int MonthlyPriceCents { get; init; }
    public int? YearlyPriceCents { get; init; }
    public int MaxUsers { get; init; }
    public int MaxDevices { get; init; }
    public string StripePriceId { get; init; } = string.Empty;
    public PlanFeatureSet Features { get; init; } = new();
}

public record CheckoutSessionResponse
{
    public string CheckoutUrl { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
}

public record CustomerPortalResponse
{
    public string PortalUrl { get; init; } = string.Empty;
}

public record InvoiceResponse
{
    public Guid Id { get; init; }
    public long AmountCents { get; init; }
    public string Currency { get; init; } = "usd";
    public string Status { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PlanName { get; init; }
    public int Quantity { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public string? PdfUrl { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? RefundStatus { get; init; }
}
