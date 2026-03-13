using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração da tabela daily_summaries para resumos diários pré-calculados
/// </summary>
internal sealed class DailySummaryConfiguration : IEntityTypeConfiguration<DailySummary>
{
    public void Configure(EntityTypeBuilder<DailySummary> builder)
    {
        builder.ToTable("daily_summaries");

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

        builder.Property(d => d.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(d => d.TotalActiveSeconds)
            .HasColumnName("total_active_seconds")
            .IsRequired();

        builder.Property(d => d.TotalIdleSeconds)
            .HasColumnName("total_idle_seconds")
            .IsRequired();

        builder.Property(d => d.SessionCount)
            .HasColumnName("session_count")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at");

        // Unique constraint: one summary per user per day
        builder.HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();

        // Index for organization queries
        builder.HasIndex(d => new { d.OrgId, d.Date });

        // Foreign key relationship
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
