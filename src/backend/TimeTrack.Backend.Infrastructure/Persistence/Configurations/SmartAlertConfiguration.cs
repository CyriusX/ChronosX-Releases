using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class SmartAlertConfiguration : IEntityTypeConfiguration<SmartAlert>
{
    public void Configure(EntityTypeBuilder<SmartAlert> builder)
    {
        builder.ToTable("smart_alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(a => a.AboutUserId)
            .HasColumnName("about_user_id");

        builder.Property(a => a.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(a => a.AlertType)
            .HasColumnName("alert_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Message)
            .HasColumnName("message")
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ActionType)
            .HasColumnName("action_type")
            .HasMaxLength(50);

        builder.Property(a => a.WasRead)
            .HasColumnName("was_read")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.WasActed)
            .HasColumnName("was_acted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(a => new { a.UserId, a.WasRead, a.CreatedAt });
        builder.HasIndex(a => new { a.OrgId, a.Severity, a.CreatedAt });
        builder.HasIndex(a => new { a.AlertType, a.UserId, a.CreatedAt });
    }
}
