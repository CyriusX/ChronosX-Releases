using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class StorageKeyConfiguration : IEntityTypeConfiguration<StorageKey>
{
    public void Configure(EntityTypeBuilder<StorageKey> builder)
    {
        builder.ToTable("storage_keys");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(s => s.Bucket)
            .HasColumnName("bucket")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Key)
            .HasColumnName("key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.Region)
            .HasColumnName("region")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(s => new { s.OrgId, s.Key });
    }
}
