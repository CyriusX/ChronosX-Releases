using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class PlatformHealthStateConfiguration : IEntityTypeConfiguration<PlatformHealthState>
{
    public void Configure(EntityTypeBuilder<PlatformHealthState> builder)
    {
        builder.ToTable("platform_health_state");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.ChecksJson)
            .HasColumnName("checks_json")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(s => s.LastChangedAtUtc)
            .HasColumnName("last_changed_at_utc")
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();
    }
}

