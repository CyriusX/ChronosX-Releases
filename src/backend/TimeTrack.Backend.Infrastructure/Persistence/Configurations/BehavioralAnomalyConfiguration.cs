using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class BehavioralAnomalyConfiguration : IEntityTypeConfiguration<BehavioralAnomaly>
{
    public void Configure(EntityTypeBuilder<BehavioralAnomaly> builder)
    {
        builder.ToTable("behavioral_anomalies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(a => a.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(a => a.AnomalyType)
            .HasColumnName("anomaly_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.DetectedAt)
            .HasColumnName("detected_at")
            .IsRequired();

        builder.Property(a => a.Evidence)
            .HasColumnName("evidence")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.BaselineValue)
            .HasColumnName("baseline_value");

        builder.Property(a => a.ActualValue)
            .HasColumnName("actual_value");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(a => new { a.UserId, a.DetectedAt });
        builder.HasIndex(a => new { a.OrgId, a.Severity, a.DetectedAt });
    }
}
