using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Integrations.Stripe;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Persistence.Seeds;

public static class SubscriptionPlanSeed
{
    private static readonly PropertyInfo[] ConfigFeatureProperties = typeof(PlanFeatures)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(bool))
        .ToArray();

    public static async Task SeedAsync(
        TimeTrackDbContext context,
        IOptions<PlansOptions> plansOptions,
        CancellationToken cancellationToken = default)
    {
        var configPlans = plansOptions.Value.Plans;
        if (configPlans.Count == 0) return;

        var existingPlans = await context.SubscriptionPlans
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Tier, cancellationToken);

        foreach (var cfg in configPlans)
        {
            if (!Enum.TryParse<PlanTier>(cfg.Tier, ignoreCase: true, out var tier))
                continue;

            var features = ToFeatureDictionary(cfg.Features);

            if (existingPlans.TryGetValue(tier, out var existing))
            {
                existing.UpdateFromConfig(
                    cfg.Name,
                    cfg.MonthlyPriceCents,
                    cfg.YearlyPriceCents,
                    cfg.MaxUsers,
                    cfg.MaxDevices,
                    features,
                    cfg.StripePriceId,
                    cfg.StripeProductId);
            }
            else
            {
                var plan = SubscriptionPlan.Create(
                    name: cfg.Name,
                    tier: tier,
                    monthlyPriceCents: cfg.MonthlyPriceCents,
                    maxUsers: cfg.MaxUsers,
                    maxDevices: cfg.MaxDevices,
                    features: features,
                    stripePriceId: cfg.StripePriceId,
                    stripeProductId: cfg.StripeProductId,
                    yearlyPriceCents: cfg.YearlyPriceCents);

                await context.SubscriptionPlans.AddAsync(plan, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, bool> ToFeatureDictionary(PlanFeatures features) =>
        ConfigFeatureProperties.ToDictionary(
            p => p.Name,
            p => (bool)p.GetValue(features)!,
            StringComparer.OrdinalIgnoreCase);
}
