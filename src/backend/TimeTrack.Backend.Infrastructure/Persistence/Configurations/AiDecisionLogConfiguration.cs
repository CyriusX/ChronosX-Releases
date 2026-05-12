using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class AiDecisionLogConfiguration : IEntityTypeConfiguration<AiDecisionLog>
{
    public void Configure(EntityTypeBuilder<AiDecisionLog> builder)
    {
        builder.ToTable("ai_decision_log");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.UserId)
            .HasColumnName("user_id");

        builder.Property(a => a.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(a => a.DecisionType)
            .HasColumnName("decision_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.InputData)
            .HasColumnName("input_data")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.Output)
            .HasColumnName("output")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Confidence)
            .HasColumnName("confidence");

        builder.Property(a => a.TokensUsed)
            .HasColumnName("tokens_used");

        builder.Property(a => a.LatencyMs)
            .HasColumnName("latency_ms");

        builder.Property(a => a.WasReviewed)
            .HasColumnName("was_reviewed")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.ReviewOutcome)
            .HasColumnName("review_outcome")
            .HasMaxLength(50);

        builder.Property(a => a.CorrectValue)
            .HasColumnName("correct_value")
            .HasColumnType("jsonb");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(a => new { a.UserId, a.DecisionType, a.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(a => new { a.OrgId, a.DecisionType, a.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(a => new { a.WasReviewed, a.DecisionType })
            .HasFilter("was_reviewed = false");
    }
}
