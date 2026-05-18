using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_items");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(e => e.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(e => e.EvidenceType)
            .HasColumnName("evidence_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ExternalMediaId)
            .HasColumnName("external_media_id")
            .HasMaxLength(100);

        builder.Property(e => e.CapturedAt)
            .HasColumnName("captured_at")
            .IsRequired();

        builder.Property(e => e.AppName)
            .HasColumnName("app_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.WindowTitleHash)
            .HasColumnName("window_title_hash")
            .HasMaxLength(128);

        builder.Property(e => e.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.OrgId, e.CapturedAt });
        builder.HasIndex(e => new { e.UserId, e.CapturedAt });
        builder.HasIndex(e => e.StorageKey).IsUnique();
        builder.HasIndex(e => new { e.IsDeleted, e.CapturedAt });
    }
}
