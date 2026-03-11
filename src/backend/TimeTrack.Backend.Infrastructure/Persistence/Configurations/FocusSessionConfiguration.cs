using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> builder)
    {
        builder.ToTable("focus_sessions");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(f => f.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(f => f.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(f => f.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(f => f.PlannedDurationMinutes)
            .HasColumnName("planned_duration_minutes");

        builder.Property(f => f.ActualDurationMinutes)
            .HasColumnName("actual_duration_minutes");

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.FocusScore)
            .HasColumnName("focus_score");

        builder.Property(f => f.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(f => f.IdempotencyKey)
            .IsUnique();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes for queries
        builder.HasIndex(f => new { f.OrgId, f.UserId, f.StartedAt });
        builder.HasIndex(f => new { f.DeviceId, f.StartedAt });
    }
}
