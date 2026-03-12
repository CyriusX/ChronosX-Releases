using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(prt => prt.Id);

        builder.Property(prt => prt.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(prt => prt.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(prt => prt.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(prt => prt.TokenHash)
            .IsUnique();

        builder.Property(prt => prt.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(prt => prt.UsedAt)
            .HasColumnName("used_at");

        builder.Property(prt => prt.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(prt => prt.UserId);

        // Relationships
        builder.HasOne(prt => prt.User)
            .WithMany()
            .HasForeignKey(prt => prt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
