namespace TimeTrack.Backend.Infrastructure.Integrations.Stripe;

public sealed class PlansOptions
{
    public List<PlanDefinition> Plans { get; init; } = [];
}

public sealed class PlanDefinition
{
    public string Tier { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? StripePriceId { get; init; }
    public string? StripeProductId { get; init; }
    public int MonthlyPriceCents { get; init; }
    public int? YearlyPriceCents { get; init; }
    public int MaxUsers { get; init; }
    public int MaxDevices { get; init; }
    public PlanFeatures Features { get; init; } = new();
}

public sealed class PlanFeatures
{
    public bool MachineMonitoring { get; init; }
    public bool AdvancedReports { get; init; }
    public bool FocusMode { get; init; }
    public bool ApiAccess { get; init; }
    public bool PrioritySupport { get; init; }
    public bool CustomCategories { get; init; }
    public bool LinearIntegration { get; init; }
    public bool BillingAnalytics { get; init; }
}
