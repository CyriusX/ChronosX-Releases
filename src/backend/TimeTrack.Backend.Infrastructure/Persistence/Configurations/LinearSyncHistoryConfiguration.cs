using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class LinearSyncHistoryConfiguration : IEntityTypeConfiguration<LinearSyncHistory>
{
    public void Configure(EntityTypeBuilder<LinearSyncHistory> builder)
    {
        builder.ToTable("linear_sync_history");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(h => h.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(h => h.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(h => h.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(h => h.FinishedAt)
            .HasColumnName("finished_at")
            .IsRequired();

        builder.Property(h => h.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(h => h.ProjectsCreated)
            .HasColumnName("projects_created")
            .IsRequired();

        builder.Property(h => h.ProjectsUpdated)
            .HasColumnName("projects_updated")
            .IsRequired();

        builder.Property(h => h.TasksCreated)
            .HasColumnName("tasks_created")
            .IsRequired();

        builder.Property(h => h.TasksUpdated)
            .HasColumnName("tasks_updated")
            .IsRequired();

        builder.Property(h => h.TasksSoftDeleted)
            .HasColumnName("tasks_soft_deleted")
            .IsRequired();

        builder.Property(h => h.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(h => h.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.HasIndex(h => new { h.UserId, h.StartedAt });
    }
}
