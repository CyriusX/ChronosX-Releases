using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class FeatureMonthlyConfiguration : IEntityTypeConfiguration<FeatureMonthly>
{
    public void Configure(EntityTypeBuilder<FeatureMonthly> builder)
    {
        builder.ToTable("feature_monthly");

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

        builder.Property(f => f.MonthStart)
            .HasColumnName("month_start")
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

        builder.Property(f => f.TrendFocusScore)
            .HasColumnName("trend_focus_score");

        builder.Property(f => f.ComputedAt)
            .HasColumnName("computed_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(f => new { f.UserId, f.MonthStart })
            .IsUnique();

        builder.HasIndex(f => new { f.OrgId, f.MonthStart });

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
