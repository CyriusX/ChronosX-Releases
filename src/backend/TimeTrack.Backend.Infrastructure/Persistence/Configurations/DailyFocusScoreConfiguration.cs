using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração da tabela daily_focus_scores para scores de foco diários
///
/// SRP: Apenas configura o mapeamento EF Core para DailyFocusScore
/// </summary>
internal sealed class DailyFocusScoreConfiguration : IEntityTypeConfiguration<DailyFocusScore>
{
    public void Configure(EntityTypeBuilder<DailyFocusScore> builder)
    {
        builder.ToTable("daily_focus_scores");

        // Primary Key
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // Foreign Keys
        builder.Property(d => d.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(d => d.DeviceId)
            .HasColumnName("device_id");

        // Date
        builder.Property(d => d.Date)
            .HasColumnName("date")
            .IsRequired();

        // Time Metrics (milliseconds)
        builder.Property(d => d.TotalTrackedMs)
            .HasColumnName("total_tracked_ms")
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(d => d.FocusTimeMs)
            .HasColumnName("focus_time_ms")
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(d => d.DistractionMs)
            .HasColumnName("distraction_ms")
            .IsRequired()
            .HasDefaultValue(0L);

        // Behavior Metrics
        builder.Property(d => d.DistractionCount)
            .HasColumnName("distraction_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.PauseCount)
            .HasColumnName("pause_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.IdleCount)
            .HasColumnName("idle_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.LongFocusBlockCount)
            .HasColumnName("long_focus_block_count")
            .IsRequired()
            .HasDefaultValue(0);

        // Score (0-100)
        builder.Property(d => d.FocusScore)
            .HasColumnName("focus_score")
            .IsRequired();

        // Timestamps
        builder.Property(d => d.CalculatedAt)
            .HasColumnName("calculated_at")
            .HasDefaultValueSql("now()");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one score per user per day per org
        builder.HasIndex(d => new { d.OrgId, d.UserId, d.Date })
            .IsUnique();

        // Index for user queries by date (most common query pattern)
        builder.HasIndex(d => new { d.OrgId, d.UserId, d.Date })
            .IsDescending(false, false, true); // Date DESC for recent-first queries

        // Index for organization-wide reports
        builder.HasIndex(d => new { d.OrgId, d.Date })
            .IsDescending(false, true); // Date DESC

        // Relationships
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Device)
            .WithMany()
            .HasForeignKey(d => d.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
