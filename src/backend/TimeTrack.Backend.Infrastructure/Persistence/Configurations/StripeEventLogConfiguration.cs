using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class StripeEventLogConfiguration : IEntityTypeConfiguration<StripeEventLog>
{
    public void Configure(EntityTypeBuilder<StripeEventLog> builder)
    {
        builder.ToTable("stripe_event_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(e => e.StripeEventId)
            .HasColumnName("stripe_event_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .HasDefaultValueSql("now()");

        builder.Property(e => e.PayloadHash)
            .HasColumnName("payload_hash")
            .HasMaxLength(64);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message");

        builder.HasIndex(e => e.StripeEventId).IsUnique();
        builder.HasIndex(e => new { e.OrgId, e.ProcessedAt });
    }
}
