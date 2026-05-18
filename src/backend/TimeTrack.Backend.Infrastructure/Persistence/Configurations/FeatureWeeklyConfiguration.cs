using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class FeatureWeeklyConfiguration : IEntityTypeConfiguration<FeatureWeekly>
{
    public void Configure(EntityTypeBuilder<FeatureWeekly> builder)
    {
        builder.ToTable("feature_weekly");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(f => f.WeekStart)
            .HasColumnName("week_start")
            .IsRequired();

        builder.Property(f => f.AvgFocusScore)
            .HasColumnName("avg_focus_score")
            .IsRequired();

        builder.Property(f => f.AvgProductiveRatio)
            .HasColumnName("avg_productive_ratio")
            .IsRequired();

        builder.Property(f => f.TotalActiveHours)
            .HasColumnName("total_active_hours")
            .IsRequired();

        builder.Property(f => f.AvgContextSwitches)
            .HasColumnName("avg_context_switches")
            .IsRequired();

        builder.Property(f => f.AvgInterruptionCount)
            .HasColumnName("avg_interruption_count")
            .IsRequired();

        builder.Property(f => f.TrendFocusScore)
            .HasColumnName("trend_focus_score");

        builder.Property(f => f.TrendProductiveRatio)
            .HasColumnName("trend_productive_ratio");

        builder.Property(f => f.ComputedAt)
            .HasColumnName("computed_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(f => new { f.UserId, f.WeekStart })
            .IsUnique();

        builder.HasIndex(f => new { f.OrgId, f.WeekStart });

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
