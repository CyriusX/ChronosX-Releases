using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class UserPatternConfiguration : IEntityTypeConfiguration<UserPattern>
{
    public void Configure(EntityTypeBuilder<UserPattern> builder)
    {
        builder.ToTable("user_patterns");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(p => p.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(p => p.PatternTag)
            .HasColumnName("pattern_tag")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.DetectedAt)
            .HasColumnName("detected_at")
            .IsRequired();

        builder.Property(p => p.Strength)
            .HasColumnName("strength")
            .IsRequired();

        builder.Property(p => p.Evidence)
            .HasColumnName("evidence")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description");

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(p => new { p.UserId, p.PatternTag, p.DetectedAt }).IsUnique();
        builder.HasIndex(p => new { p.UserId, p.IsActive, p.DetectedAt });
    }
}
