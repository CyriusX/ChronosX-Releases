using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class OpsDeviceIssueStateConfiguration : IEntityTypeConfiguration<OpsDeviceIssueState>
{
    public void Configure(EntityTypeBuilder<OpsDeviceIssueState> builder)
    {
        builder.ToTable("ops_device_issue_states");

        builder.HasKey(s => s.DeviceId);

        builder.Property(s => s.DeviceId)
            .HasColumnName("device_id");

        builder.Property(s => s.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(s => s.Issue)
            .HasColumnName("issue")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(s => s.LastTransitionAtUtc)
            .HasColumnName("last_transition_at_utc")
            .IsRequired();

        builder.Property(s => s.LastNotifiedAtUtc)
            .HasColumnName("last_notified_at_utc");

        builder.HasIndex(s => s.OrgId)
            .HasDatabaseName("ix_ops_device_issue_states_org_id");
    }
}

