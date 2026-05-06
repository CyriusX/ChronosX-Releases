using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class OrgSubscriptionConfiguration : IEntityTypeConfiguration<OrgSubscription>
{
    public void Configure(EntityTypeBuilder<OrgSubscription> builder)
    {
        builder.ToTable("org_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(s => s.PlanId)
            .HasColumnName("plan_id");

        builder.Property(s => s.StripeCustomerId)
            .HasColumnName("stripe_customer_id")
            .HasMaxLength(200);

        builder.Property(s => s.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(200);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStart)
            .HasColumnName("current_period_start");

        builder.Property(s => s.CurrentPeriodEnd)
            .HasColumnName("current_period_end");

        builder.Property(s => s.TrialEnd)
            .HasColumnName("trial_end");

        builder.Property(s => s.GracePeriodEnd)
            .HasColumnName("grace_period_end");

        builder.Property(s => s.CanceledAt)
            .HasColumnName("canceled_at");

        builder.Property(s => s.CancelAtPeriodEnd)
            .HasColumnName("cancel_at_period_end")
            .HasDefaultValue(false);

        builder.Property(s => s.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(s => s.OrgId).IsUnique();
        builder.HasIndex(s => s.StripeCustomerId);
        builder.HasIndex(s => s.StripeSubscriptionId);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Organization)
            .WithMany()
            .HasForeignKey(s => s.OrgId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
