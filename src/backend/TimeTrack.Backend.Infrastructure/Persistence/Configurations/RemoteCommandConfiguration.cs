using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class RemoteCommandConfiguration : IEntityTypeConfiguration<RemoteCommand>
{
    public void Configure(EntityTypeBuilder<RemoteCommand> builder)
    {
        builder.ToTable("remote_commands");

        builder.HasKey(rc => rc.Id);

        builder.Property(rc => rc.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(rc => rc.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(rc => rc.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(rc => rc.CommandType)
            .HasColumnName("command_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(rc => rc.PayloadJson)
            .HasColumnName("payload_json")
            .HasMaxLength(2000);

        builder.Property(rc => rc.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rc => rc.ResultJson)
            .HasColumnName("result_json")
            .HasMaxLength(2000);

        builder.Property(rc => rc.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(rc => rc.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(rc => rc.AcknowledgedAt)
            .HasColumnName("acknowledged_at");

        builder.Property(rc => rc.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(rc => new { rc.DeviceId, rc.Status })
            .HasDatabaseName("ix_remote_commands_device_status");

        builder.HasIndex(rc => new { rc.OrgId, rc.DeviceId, rc.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_remote_commands_org_device_created");

        // Relationships
        builder.HasOne(rc => rc.Device)
            .WithMany()
            .HasForeignKey(rc => rc.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
