namespace TimeTrack.Backend.Application.Common.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionCheckResult> CheckSubscriptionAccessAsync(Guid orgId, CancellationToken ct = default);
    Task<bool> IsFeatureEnabledAsync(Guid orgId, string feature, CancellationToken ct = default);
    Task EnforceSeatLimitAsync(Guid orgId, int requestedSeats, CancellationToken ct = default);
}

public record SubscriptionCheckResult
{
    public bool HasAccess { get; init; }
    public string Status { get; init; } = "none";
    public string? PlanTier { get; init; }
    public string? PlanName { get; init; }
    public bool IsInGracePeriod { get; init; }
    public DateTime? GracePeriodEnd { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
}
