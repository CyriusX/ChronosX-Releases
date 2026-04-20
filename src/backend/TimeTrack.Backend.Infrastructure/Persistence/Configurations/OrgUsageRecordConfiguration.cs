using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class OrgUsageRecordConfiguration : IEntityTypeConfiguration<OrgUsageRecord>
{
    public void Configure(EntityTypeBuilder<OrgUsageRecord> builder)
    {
        builder.ToTable("org_usage_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(r => r.ActiveUsersCount)
            .HasColumnName("active_users_count")
            .HasDefaultValue(0);

        builder.Property(r => r.ActiveDevicesCount)
            .HasColumnName("active_devices_count")
            .HasDefaultValue(0);

        builder.Property(r => r.LastComputedAt)
            .HasColumnName("last_computed_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(r => r.OrgId).IsUnique();
    }
}
