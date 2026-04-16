using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(t => t.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(5000);

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.AssignedUserId)
            .HasColumnName("assigned_user_id");

        builder.Property(t => t.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.DueDate)
            .HasColumnName("due_date");

        builder.Property(t => t.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(t => t.MovedToInProgressAt)
            .HasColumnName("moved_to_in_progress_at");

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.TotalSecondsWorked)
            .HasColumnName("total_seconds_worked")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        // Optimistic concurrency token (PostgreSQL xmin)
        builder.Property(t => t.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Linear mirror columns ──
        builder.Property(t => t.LinearIssueId)
            .HasColumnName("linear_issue_id")
            .HasMaxLength(64);

        builder.Property(t => t.LinearIssueIdentifier)
            .HasColumnName("linear_issue_identifier")
            .HasMaxLength(32);

        builder.Property(t => t.LinearUrl)
            .HasColumnName("linear_url")
            .HasMaxLength(500);

        builder.Property(t => t.LinearStateId)
            .HasColumnName("linear_state_id")
            .HasMaxLength(64);

        builder.Property(t => t.LinearStateName)
            .HasColumnName("linear_state_name")
            .HasMaxLength(100);

        builder.Property(t => t.LinearTeamId)
            .HasColumnName("linear_team_id")
            .HasMaxLength(64);

        builder.Ignore(t => t.IsLinearSourced);

        // Hot-path indexes (kanban + dashboard widgets):
        // - Per-project columns sorted by position
        // - Per-user assigned tasks by status (also sorted by position)
        builder.HasIndex(t => new { t.OrgId, t.ProjectId, t.Status, t.Position })
            .HasDatabaseName("IX_project_tasks_org_id_project_id_status_position");
        builder.HasIndex(t => new { t.AssignedUserId, t.Status, t.Position })
            .HasDatabaseName("IX_project_tasks_assigned_user_id_status_position");
        builder.HasIndex(t => new { t.OrgId, t.LinearIssueId })
            .IsUnique()
            .HasFilter("linear_issue_id IS NOT NULL");

        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
