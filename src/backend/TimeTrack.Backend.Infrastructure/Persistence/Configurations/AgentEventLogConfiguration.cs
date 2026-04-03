using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class AgentEventLogConfiguration : IEntityTypeConfiguration<AgentEventLog>
{
    public void Configure(EntityTypeBuilder<AgentEventLog> builder)
    {
        builder.ToTable("agent_event_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(e => e.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Message)
            .HasColumnName("message")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasColumnName("metadata_json")
            .HasMaxLength(4000);

        builder.Property(e => e.TimestampUtc)
            .HasColumnName("timestamp_utc")
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(e => new { e.DeviceId, e.TimestampUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_agent_event_logs_device_timestamp");

        builder.HasIndex(e => e.OrgId)
            .HasDatabaseName("ix_agent_event_logs_org_id");

        builder.HasIndex(e => e.Severity)
            .HasDatabaseName("ix_agent_event_logs_severity");

        builder.HasIndex(e => new { e.Category, e.EventType })
            .HasDatabaseName("ix_agent_event_logs_category_type");

        builder.HasIndex(e => e.TimestampUtc)
            .HasDatabaseName("ix_agent_event_logs_timestamp");

        // Relationships
        builder.HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
