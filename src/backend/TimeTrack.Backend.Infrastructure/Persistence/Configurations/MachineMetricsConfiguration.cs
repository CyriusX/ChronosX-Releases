using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class MachineMetricsConfiguration : IEntityTypeConfiguration<MachineMetrics>
{
    public void Configure(EntityTypeBuilder<MachineMetrics> builder)
    {
        builder.ToTable("machine_metrics");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(m => m.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(m => m.CpuPercent)
            .HasColumnName("cpu_percent")
            .IsRequired();

        builder.Property(m => m.MemoryUsedMb)
            .HasColumnName("memory_used_mb")
            .IsRequired();

        builder.Property(m => m.MemoryTotalMb)
            .HasColumnName("memory_total_mb")
            .IsRequired();

        builder.Property(m => m.DiskUsedGb)
            .HasColumnName("disk_used_gb")
            .IsRequired();

        builder.Property(m => m.DiskTotalGb)
            .HasColumnName("disk_total_gb")
            .IsRequired();

        builder.Property(m => m.SampledAtUtc)
            .HasColumnName("sampled_at_utc")
            .IsRequired();

        builder.Property(m => m.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(m => new { m.DeviceId, m.SampledAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_machine_metrics_device_sampled");

        builder.HasIndex(m => m.OrgId)
            .HasDatabaseName("ix_machine_metrics_org_id");

        builder.HasIndex(m => m.SampledAtUtc)
            .HasDatabaseName("ix_machine_metrics_sampled_at");

        // Relationships
        builder.HasOne(m => m.Device)
            .WithMany()
            .HasForeignKey(m => m.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
