namespace TimeTrack.Backend.Infrastructure.Integrations.Stripe;

/// <summary>
/// Subscription/trial configuration, bound to the "Subscription" section of
/// appsettings. Keep trial-related knobs here so ops can tune them without
/// a code change.
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>Length of the free trial granted on signup, in days. Default 14.</summary>
    public int TrialDays { get; init; } = 14;
}
