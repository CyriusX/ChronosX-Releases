using System.Reflection;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class SubscriptionPlan
{
    private static readonly PropertyInfo[] BooleanProperties = typeof(SubscriptionPlan)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite)
        .ToArray();

    private static readonly HashSet<string> FeatureNames = new(
        BooleanProperties.Select(p => p.Name),
        StringComparer.OrdinalIgnoreCase);

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? StripePriceId { get; private set; }
    public string? StripeProductId { get; private set; }
    public PlanTier Tier { get; private set; }
    public int MonthlyPriceCents { get; private set; }
    public int? YearlyPriceCents { get; private set; }
    public int MaxUsers { get; private set; }
    public int MaxDevices { get; private set; }

    // Feature flags — add new ones here. FeatureMap, seed, and DTO pick them up automatically.
    public bool MachineMonitoring { get; private set; }
    public bool AdvancedReports { get; private set; }
    public bool FocusMode { get; private set; }
    public bool ApiAccess { get; private set; }
    public bool PrioritySupport { get; private set; }
    public bool CustomCategories { get; private set; }
    public bool LinearIntegration { get; private set; }
    public bool BillingAnalytics { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool IsFeatureEnabled(string featureName)
    {
        var prop = BooleanProperties.FirstOrDefault(p =>
            string.Equals(p.Name, featureName, StringComparison.OrdinalIgnoreCase));
        return prop is not null && (bool)prop.GetValue(this)!;
    }

    public static bool IsValidFeatureName(string featureName) =>
        FeatureNames.Contains(featureName);

    public static IReadOnlySet<string> GetFeatureNames() => FeatureNames;

    public Dictionary<string, bool> ToFeatureDictionary() =>
        BooleanProperties.ToDictionary(
            p => p.Name,
            p => (bool)p.GetValue(this)!,
            StringComparer.OrdinalIgnoreCase);

    private SubscriptionPlan() { }

    public static SubscriptionPlan Create(
        string name,
        PlanTier tier,
        int monthlyPriceCents,
        int maxUsers,
        int maxDevices,
        Dictionary<string, bool> features,
        string? stripePriceId = null,
        string? stripeProductId = null,
        int? yearlyPriceCents = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan name is required", nameof(name));

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            MonthlyPriceCents = monthlyPriceCents,
            YearlyPriceCents = yearlyPriceCents,
            MaxUsers = maxUsers,
            MaxDevices = maxDevices,
            StripePriceId = stripePriceId,
            StripeProductId = stripeProductId,
            CreatedAt = DateTime.UtcNow
        };

        ApplyFeatures(plan, features);
        return plan;
    }

    public void UpdateFromConfig(
        string name,
        int monthlyPriceCents,
        int? yearlyPriceCents,
        int maxUsers,
        int maxDevices,
        Dictionary<string, bool> features,
        string? stripePriceId,
        string? stripeProductId)
    {
        Name = name;
        MonthlyPriceCents = monthlyPriceCents;
        YearlyPriceCents = yearlyPriceCents;
        MaxUsers = maxUsers;
        MaxDevices = maxDevices;
        StripePriceId = stripePriceId;
        StripeProductId = stripeProductId;
        ApplyFeatures(this, features);
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyFeatures(SubscriptionPlan plan, Dictionary<string, bool> features)
    {
        foreach (var prop in BooleanProperties)
        {
            if (features.TryGetValue(prop.Name, out var value))
            {
                prop.SetValue(plan, value);
            }
        }
    }

    public void UpdateStripeIds(string stripePriceId, string stripeProductId)
    {
        StripePriceId = stripePriceId;
        StripeProductId = stripeProductId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateYearlyPrice(int yearlyPriceCents)
    {
        YearlyPriceCents = yearlyPriceCents;
        UpdatedAt = DateTime.UtcNow;
    }
}
