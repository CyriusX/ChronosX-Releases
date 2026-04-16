using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class TaskTimeEntryConfiguration : IEntityTypeConfiguration<TaskTimeEntry>
{
    public void Configure(EntityTypeBuilder<TaskTimeEntry> builder)
    {
        builder.ToTable("task_time_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(e => e.TaskId)
            .HasColumnName("task_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(e => e.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds")
            .IsRequired();

        builder.Property(e => e.PausedSeconds)
            .HasColumnName("paused_seconds")
            .IsRequired();

        builder.Property(e => e.PausedAt)
            .HasColumnName("paused_at");

        builder.Property(e => e.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Critical index — finding the open entry for a user is the hottest query.
        builder.HasIndex(e => new { e.UserId, e.EndedAt });
        // Enforce at most one open entry per user at the database level (partial unique index).
        // This is the invariant the application code assumes.
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("IX_task_time_entries_user_id_open")
            .HasFilter("ended_at IS NULL");
        builder.HasIndex(e => new { e.OrgId, e.TaskId, e.StartedAt });

        builder.HasOne(e => e.Task)
            .WithMany()
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
