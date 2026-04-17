using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TimeTrack.Backend.Infrastructure.Persistence;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TimeTrackDbContext))]
[Migration("20260417120000_AddProjectCreatorAndSoftDelete")]
public partial class AddProjectCreatorAndSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS created_by_user_id uuid;");
        migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;");
        migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_by_user_id uuid;");

        // Speeds up the daily purge job.
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_projects_deleted_at ON projects (deleted_at);");

        // Backfill CreatedByUserId for legacy projects:
        // - prefer earliest Owner member
        // - fallback to earliest member if no Owner
        // - if a project has no members, leave NULL (treated as 'not creator' for non-admin permissions)
        migrationBuilder.Sql(@"
            UPDATE projects p
            SET created_by_user_id = (
                SELECT pm.user_id
                FROM project_members pm
                WHERE pm.project_id = p.id
                ORDER BY (pm.role = 'Owner') DESC, pm.added_at ASC, pm.id ASC
                LIMIT 1
            )
            WHERE p.created_by_user_id IS NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_projects_deleted_at;");

        migrationBuilder.DropColumn(name: "deleted_by_user_id", table: "projects");
        migrationBuilder.DropColumn(name: "deleted_at", table: "projects");
        migrationBuilder.DropColumn(name: "created_by_user_id", table: "projects");
    }
}
