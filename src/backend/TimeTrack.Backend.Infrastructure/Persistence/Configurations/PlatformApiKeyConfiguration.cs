using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class PlatformApiKeyConfiguration : IEntityTypeConfiguration<PlatformApiKey>
{
    public void Configure(EntityTypeBuilder<PlatformApiKey> builder)
    {
        builder.ToTable("platform_api_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(k => k.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(k => k.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(k => k.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(k => k.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(k => k.LastUsedAtUtc)
            .HasColumnName("last_used_at_utc");

        builder.HasIndex(k => k.KeyHash)
            .IsUnique()
            .HasDatabaseName("ux_platform_api_keys_key_hash");

        builder.HasIndex(k => k.CreatedAtUtc)
            .HasDatabaseName("ix_platform_api_keys_created_at_utc");
    }
}

