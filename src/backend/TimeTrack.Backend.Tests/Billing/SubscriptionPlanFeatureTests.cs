using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Billing;

public class SubscriptionPlanFeatureTests
{
    private static SubscriptionPlan CreatePlan(Dictionary<string, bool>? overrides = null)
    {
        var features = new Dictionary<string, bool>
        {
            ["MachineMonitoring"] = true,
            ["AdvancedReports"] = true,
            ["FocusMode"] = true,
            ["ApiAccess"] = true,
            ["PrioritySupport"] = false,
            ["CustomCategories"] = true,
            ["LinearIntegration"] = true,
            ["BillingAnalytics"] = false,
        };

        if (overrides is not null)
        {
            foreach (var kvp in overrides)
                features[kvp.Key] = kvp.Value;
        }

        return SubscriptionPlan.Create("Test Plan", PlanTier.Pro, 1200, 25, 50, features);
    }

    [Fact]
    public void IsFeatureEnabled_ReturnsTrue_WhenFeatureIsEnabled()
    {
        var plan = CreatePlan();

        plan.IsFeatureEnabled("MachineMonitoring").Should().BeTrue();
        plan.IsFeatureEnabled("AdvancedReports").Should().BeTrue();
        plan.IsFeatureEnabled("FocusMode").Should().BeTrue();
    }

    [Fact]
    public void IsFeatureEnabled_ReturnsFalse_WhenFeatureIsDisabled()
    {
        var plan = CreatePlan(new Dictionary<string, bool>
        {
            ["PrioritySupport"] = false,
            ["BillingAnalytics"] = false,
        });

        plan.IsFeatureEnabled("PrioritySupport").Should().BeFalse();
        plan.IsFeatureEnabled("BillingAnalytics").Should().BeFalse();
    }

    [Fact]
    public void IsFeatureEnabled_ReturnsFalse_ForUnknownFeature()
    {
        var plan = CreatePlan();

        plan.IsFeatureEnabled("AiAccess").Should().BeFalse();
        plan.IsFeatureEnabled("NonExistentFeature").Should().BeFalse();
    }

    [Fact]
    public void IsFeatureEnabled_IsCaseInsensitive()
    {
        var plan = CreatePlan();

        plan.IsFeatureEnabled("machinemonitoring").Should().BeTrue();
        plan.IsFeatureEnabled("MACHINEMONITORING").Should().BeTrue();
        plan.IsFeatureEnabled("MachineMonitoring").Should().BeTrue();
    }

    [Fact]
    public void IsValidFeatureName_ReturnsTrue_ForAllKnownFeatures()
    {
        var featureNames = SubscriptionPlan.GetFeatureNames();

        featureNames.Should().Contain(
        [
            "MachineMonitoring", "AdvancedReports", "FocusMode", "ApiAccess",
            "PrioritySupport", "CustomCategories", "LinearIntegration", "BillingAnalytics"
        ]);
    }

    [Fact]
    public void IsValidFeatureName_ReturnsFalse_ForUnknownFeature()
    {
        SubscriptionPlan.IsValidFeatureName("AiAccess").Should().BeFalse();
        SubscriptionPlan.IsValidFeatureName("Unknown").Should().BeFalse();
    }

    [Fact]
    public void ToFeatureDictionary_ReturnsAllFeatures()
    {
        var plan = CreatePlan();

        var dict = plan.ToFeatureDictionary();

        dict.Should().NotBeNull();
        dict["MachineMonitoring"].Should().BeTrue();
        dict["PrioritySupport"].Should().BeFalse();
        dict["BillingAnalytics"].Should().BeFalse();
        dict.Should().HaveCount(SubscriptionPlan.GetFeatureNames().Count);
    }

    [Fact]
    public void ToFeatureDictionary_IsCaseInsensitive()
    {
        var plan = CreatePlan();

        var dict = plan.ToFeatureDictionary();

        dict.TryGetValue("machinemonitoring", out var val).Should().BeTrue();
        val.Should().BeTrue();
    }

    [Fact]
    public void FeatureMap_AutoDiscoversBooleanProperties()
    {
        // If someone adds a new boolean property to SubscriptionPlan,
        // it should appear in GetFeatureNames() automatically.
        var featureNames = SubscriptionPlan.GetFeatureNames();

        featureNames.Count.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void Create_WithUnknownFeatureKeys_IgnoresThemGracefully()
    {
        var features = new Dictionary<string, bool>
        {
            ["MachineMonitoring"] = true,
            ["AdvancedReports"] = true,
            ["FocusMode"] = true,
            ["ApiAccess"] = true,
            ["PrioritySupport"] = false,
            ["CustomCategories"] = true,
            ["LinearIntegration"] = true,
            ["BillingAnalytics"] = false,
            ["FutureFeatureThatDoesNotExistYet"] = true,
        };

        var act = () => SubscriptionPlan.Create("Test", PlanTier.Pro, 1200, 25, 50, features);

        act.Should().NotThrow();
    }
}
