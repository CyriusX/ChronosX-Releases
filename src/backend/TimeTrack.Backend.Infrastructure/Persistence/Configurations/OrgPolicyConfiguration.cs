using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class OrgPolicyConfiguration : IEntityTypeConfiguration<OrgPolicy>
{
    public void Configure(EntityTypeBuilder<OrgPolicy> builder)
    {
        builder.ToTable("org_policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(p => p.Version)
            .HasColumnName("version")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(p => p.WorkHoursJson)
            .HasColumnName("work_hours_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.AppExclusionsJson)
            .HasColumnName("app_exclusions_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.IdleThresholdSeconds)
            .HasColumnName("idle_threshold_seconds")
            .HasDefaultValue(180)
            .IsRequired();

        builder.Property(p => p.RetentionDays)
            .HasColumnName("retention_days")
            .HasDefaultValue(90)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        // Note: We don't configure the relationship here because Organization
        // doesn't have a navigation property to OrgPolicy. The foreign key is
        // simply OrgId which references the orgs table.

        // Indexes
        builder.HasIndex(p => p.OrgId)
            .IsUnique();

        builder.HasIndex(p => p.Version);
    }
}
