using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Persistence.Seeds;

public static class SubscriptionPlanSeed
{
    public static async Task SeedAsync(
        TimeTrackDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var existingFree = await context.SubscriptionPlans
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Tier == PlanTier.Free, cancellationToken);

        if (existingFree) return;

        var freePlan = SubscriptionPlan.Create(
            name: "Free",
            tier: PlanTier.Free,
            monthlyPriceCents: 0,
            maxUsers: 3,
            maxDevices: 3,
            features: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["MachineMonitoring"] = true,
                ["AdvancedReports"] = false,
                ["FocusMode"] = true,
                ["ApiAccess"] = false,
                ["PrioritySupport"] = false,
                ["CustomCategories"] = false,
                ["LinearIntegration"] = false,
                ["BillingAnalytics"] = false,
            });

        await context.SubscriptionPlans.AddAsync(freePlan, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded Free subscription plan");
    }
}
