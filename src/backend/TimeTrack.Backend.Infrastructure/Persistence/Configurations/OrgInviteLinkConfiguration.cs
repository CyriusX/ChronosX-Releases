using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class OrgInviteLinkConfiguration : IEntityTypeConfiguration<OrgInviteLink>
{
    public void Configure(EntityTypeBuilder<OrgInviteLink> builder)
    {
        builder.ToTable("org_invite_links");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(l => l.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(l => l.TokenHash)
            .IsUnique();

        builder.Property(l => l.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(l => l.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(l => l.MaxUses)
            .HasColumnName("max_uses");

        builder.Property(l => l.UseCount)
            .HasColumnName("use_count")
            .HasDefaultValue(0);

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(l => l.OrgId);

        // Relationships
        builder.HasOne(l => l.Organization)
            .WithMany()
            .HasForeignKey(l => l.OrgId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
