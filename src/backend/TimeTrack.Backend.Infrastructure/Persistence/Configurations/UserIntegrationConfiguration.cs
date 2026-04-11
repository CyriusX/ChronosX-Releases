using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class UserIntegrationConfiguration : IEntityTypeConfiguration<UserIntegration>
{
    public void Configure(EntityTypeBuilder<UserIntegration> builder)
    {
        builder.ToTable("user_integrations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(i => i.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(i => i.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.ExternalUserId)
            .HasColumnName("external_user_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.ExternalUserName)
            .HasColumnName("external_user_name")
            .HasMaxLength(255);

        builder.Property(i => i.ExternalUserEmail)
            .HasColumnName("external_user_email")
            .HasMaxLength(255);

        builder.Property(i => i.EncryptedToken)
            .HasColumnName("encrypted_token")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(i => i.Scope)
            .HasColumnName("scope")
            .HasMaxLength(500);

        // ── OAuth fields ──
        builder.Property(i => i.AuthMethod)
            .HasColumnName("auth_method")
            .HasConversion<int>()
            .HasDefaultValue(UserIntegrationAuthMethod.ApiKey)
            .IsRequired();

        builder.Property(i => i.RefreshToken)
            .HasColumnName("refresh_token")
            .HasColumnType("bytea");

        builder.Property(i => i.TokenExpiresAt)
            .HasColumnName("token_expires_at");

        // ── One integration per (user, provider) pair ──
        builder.HasIndex(i => new { i.UserId, i.Provider })
            .IsUnique();

        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
