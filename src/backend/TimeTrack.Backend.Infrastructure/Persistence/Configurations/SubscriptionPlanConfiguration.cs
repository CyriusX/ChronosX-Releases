using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.StripePriceId)
            .HasColumnName("stripe_price_id")
            .HasMaxLength(200);

        builder.Property(p => p.StripeProductId)
            .HasColumnName("stripe_product_id")
            .HasMaxLength(200);

        builder.Property(p => p.Tier)
            .HasColumnName("tier")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.MonthlyPriceCents)
            .HasColumnName("monthly_price_cents")
            .IsRequired();

        builder.Property(p => p.YearlyPriceCents)
            .HasColumnName("yearly_price_cents");

        builder.Property(p => p.MaxUsers)
            .HasColumnName("max_users")
            .IsRequired();

        builder.Property(p => p.MaxDevices)
            .HasColumnName("max_devices")
            .IsRequired();

        builder.Property(p => p.MachineMonitoring)
            .HasColumnName("machine_monitoring")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.AdvancedReports)
            .HasColumnName("advanced_reports")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.FocusMode)
            .HasColumnName("focus_mode")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.ApiAccess)
            .HasColumnName("api_access")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.PrioritySupport)
            .HasColumnName("priority_support")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CustomCategories)
            .HasColumnName("custom_categories")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.LinearIntegration)
            .HasColumnName("linear_integration")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.BillingAnalytics)
            .HasColumnName("billing_analytics")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(p => p.Tier).IsUnique();
    }
}
