using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class PlatformEventLogConfiguration : IEntityTypeConfiguration<PlatformEventLog>
{
    public void Configure(EntityTypeBuilder<PlatformEventLog> builder)
    {
        builder.ToTable("platform_event_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
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

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(e => e.TimestampUtc)
            .HasDatabaseName("ix_platform_event_logs_timestamp");

        builder.HasIndex(e => e.Severity)
            .HasDatabaseName("ix_platform_event_logs_severity");

        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_platform_event_logs_idempotency_key");
    }
}

