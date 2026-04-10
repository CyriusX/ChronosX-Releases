using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(d => d.Hostname)
            .HasColumnName("hostname")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.DeviceName)
            .HasColumnName("device_name")
            .HasMaxLength(255);

        builder.Property(d => d.AgentVersion)
            .HasColumnName("agent_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DisplayMode)
            .HasColumnName("display_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.ActivatedAt)
            .HasColumnName("activated_at")
            .HasDefaultValueSql("now()");

        builder.Property(d => d.LastHeartbeatAt)
            .HasColumnName("last_heartbeat_at");

        builder.Property(d => d.OsVersion)
            .HasColumnName("os_version")
            .HasMaxLength(200);

        builder.Property(d => d.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(d => d.UptimeSeconds)
            .HasColumnName("uptime_seconds");

        builder.Property(d => d.TrackingState)
            .HasColumnName("tracking_state")
            .HasMaxLength(20);

        builder.Property(d => d.HealthStatus)
            .HasColumnName("health_status")
            .HasMaxLength(20);

        builder.Property(d => d.ConsecutiveSyncFailures)
            .HasColumnName("consecutive_sync_failures");

        builder.Property(d => d.LastSuccessfulSyncAt)
            .HasColumnName("last_successful_sync_at");

        builder.Property(d => d.IpcConnected)
            .HasColumnName("ipc_connected");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(d => new { d.OrgId, d.UserId });

        // Relationships
        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
