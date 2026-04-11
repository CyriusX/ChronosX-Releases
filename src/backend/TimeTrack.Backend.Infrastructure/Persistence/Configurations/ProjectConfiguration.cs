using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // Unique project names per org — but only for local projects. Linear-sourced
        // projects are keyed by LinearProjectId (see index below) and may collide on
        // name with a local project without issue.
        builder.HasIndex(p => new { p.OrgId, p.Name })
            .IsUnique()
            .HasFilter("sync_source = 'Local'");

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(p => p.Color)
            .HasColumnName("color")
            .HasMaxLength(7)
            .HasDefaultValue("#4A9FFF")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        // ── Linear sync columns ──
        builder.Property(p => p.SyncSource)
            .HasColumnName("sync_source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Domain.ValueObjects.ProjectSyncSource.Local)
            .IsRequired();

        builder.Property(p => p.LinearProjectId)
            .HasColumnName("linear_project_id")
            .HasMaxLength(64);

        builder.Property(p => p.LinearWorkspaceId)
            .HasColumnName("linear_workspace_id")
            .HasMaxLength(64);

        builder.Property(p => p.LastSyncedAt)
            .HasColumnName("last_synced_at");

        builder.HasIndex(p => new { p.OrgId, p.LinearProjectId })
            .IsUnique()
            .HasFilter("linear_project_id IS NOT NULL");

        // Relationships
        builder.HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
