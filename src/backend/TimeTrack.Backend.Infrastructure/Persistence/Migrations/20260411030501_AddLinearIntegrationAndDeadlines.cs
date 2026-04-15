using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinearIntegrationAndDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────
            // Idempotent column additions (matches AddTasksAndMembers +
            // AddDeviceHealthFields pattern — the prod DB may already have
            // these columns if a deployment applied them out of band).
            // ─────────────────────────────────────────────────────────────

            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS last_synced_at timestamp with time zone;");
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_project_id character varying(64);");
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_workspace_id character varying(64);");
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS sync_source character varying(20) NOT NULL DEFAULT 'Local';");

            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_id character varying(64);");
            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_identifier character varying(32);");
            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_id character varying(64);");
            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_name character varying(100);");
            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_team_id character varying(64);");
            migrationBuilder.Sql("ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_url character varying(500);");

            // ─────────────────────────────────────────────────────────────
            // Replace the old unique index on projects(org_id, name) with
            // a filtered index that only enforces uniqueness for local
            // projects. Linear-synced projects are keyed by linear_project_id
            // and may collide on name with a pre-existing local project.
            // ─────────────────────────────────────────────────────────────

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_projects_org_id_name\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_projects_org_id_name\" " +
                "ON projects (org_id, name) WHERE sync_source = 'Local';");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_projects_org_id_linear_project_id\" " +
                "ON projects (org_id, linear_project_id) WHERE linear_project_id IS NOT NULL;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_project_tasks_org_id_linear_issue_id\" " +
                "ON project_tasks (org_id, linear_issue_id) WHERE linear_issue_id IS NOT NULL;");

            // ─────────────────────────────────────────────────────────────
            // New tables — these don't exist in prod so regular CreateTable
            // is safe.
            // ─────────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "linear_sync_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    projects_created = table.Column<int>(type: "integer", nullable: false),
                    projects_updated = table.Column<int>(type: "integer", nullable: false),
                    tasks_created = table.Column<int>(type: "integer", nullable: false),
                    tasks_updated = table.Column<int>(type: "integer", nullable: false),
                    tasks_soft_deleted = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linear_sync_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    external_user_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encrypted_token = table.Column<byte[]>(type: "bytea", nullable: false),
                    scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_integrations", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_integrations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_linear_sync_history_user_id_started_at",
                table: "linear_sync_history",
                columns: new[] { "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_integrations_user_id_provider",
                table: "user_integrations",
                columns: new[] { "user_id", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "linear_sync_history");

            migrationBuilder.DropTable(
                name: "user_integrations");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_projects_org_id_linear_project_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_project_tasks_org_id_linear_issue_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_projects_org_id_name\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_projects_org_id_name\" " +
                "ON projects (org_id, name);");

            migrationBuilder.DropColumn(name: "last_synced_at", table: "projects");
            migrationBuilder.DropColumn(name: "linear_project_id", table: "projects");
            migrationBuilder.DropColumn(name: "linear_workspace_id", table: "projects");
            migrationBuilder.DropColumn(name: "sync_source", table: "projects");

            migrationBuilder.DropColumn(name: "linear_issue_id", table: "project_tasks");
            migrationBuilder.DropColumn(name: "linear_issue_identifier", table: "project_tasks");
            migrationBuilder.DropColumn(name: "linear_state_id", table: "project_tasks");
            migrationBuilder.DropColumn(name: "linear_state_name", table: "project_tasks");
            migrationBuilder.DropColumn(name: "linear_team_id", table: "project_tasks");
            migrationBuilder.DropColumn(name: "linear_url", table: "project_tasks");
        }
    }
}
