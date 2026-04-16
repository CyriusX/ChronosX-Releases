using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TimeTrack.Backend.Infrastructure.Persistence;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TimeTrackDbContext))]
[Migration("20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes")]
public partial class AddTaskEntryOpenUniqueAndTaskIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1) Data fix: ensure there is at most one open task_time_entry per user
        // so the new unique filtered index can be created safely.
        migrationBuilder.Sql(@"
            WITH ranked AS (
                SELECT
                    id,
                    user_id,
                    ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY started_at DESC, id DESC) AS rn
                FROM task_time_entries
                WHERE ended_at IS NULL
            )
            UPDATE task_time_entries t
            SET
                ended_at = t.started_at,
                duration_seconds = 0,
                paused_seconds = 0,
                paused_at = NULL
            FROM ranked r
            WHERE t.id = r.id
              AND r.rn > 1;
        ");

        // 2) Enforce invariant: one open entry per user
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_task_time_entries_user_id_open""
            ON task_time_entries (user_id)
            WHERE ended_at IS NULL;
        ");

        // 3) Performance: add position-aware indexes for kanban ordering.
        // Drop the older, less selective indexes to avoid planner confusion / bloat.
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS ""IX_project_tasks_assigned_user_id_status"";
            DROP INDEX IF EXISTS ""IX_project_tasks_org_id_project_id_status"";

            CREATE INDEX IF NOT EXISTS ""IX_project_tasks_assigned_user_id_status_position""
            ON project_tasks (assigned_user_id, status, position);

            CREATE INDEX IF NOT EXISTS ""IX_project_tasks_org_id_project_id_status_position""
            ON project_tasks (org_id, project_id, status, position);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS ""IX_task_time_entries_user_id_open"";

            DROP INDEX IF EXISTS ""IX_project_tasks_assigned_user_id_status_position"";
            DROP INDEX IF EXISTS ""IX_project_tasks_org_id_project_id_status_position"";

            CREATE INDEX IF NOT EXISTS ""IX_project_tasks_assigned_user_id_status""
            ON project_tasks (assigned_user_id, status);

            CREATE INDEX IF NOT EXISTS ""IX_project_tasks_org_id_project_id_status""
            ON project_tasks (org_id, project_id, status);
        ");
    }
}

