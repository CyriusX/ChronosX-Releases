using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS so the migration is idempotent
            // (matches the pattern used in 20260409224255_AddDeviceHealthFields).
            migrationBuilder.Sql("ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS project_id uuid;");
            migrationBuilder.Sql("ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS task_id uuid;");

            migrationBuilder.CreateTable(
                name: "agent_notification_inbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_to_agent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_notification_inbox", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_notification_inbox_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    moved_to_in_progress_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_seconds_worked = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_tasks_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_time_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    paused_seconds = table.Column<long>(type: "bigint", nullable: false),
                    paused_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_time_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_time_entries_project_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "project_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_time_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_project_id",
                table: "activity_sessions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_task_id",
                table: "activity_sessions",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_notification_inbox_org_id_user_id_created_at",
                table: "agent_notification_inbox",
                columns: new[] { "org_id", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_notification_inbox_user_id_read_at",
                table: "agent_notification_inbox",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_org_id_user_id",
                table: "project_members",
                columns: new[] { "org_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_project_id_user_id",
                table: "project_members",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_members_user_id",
                table: "project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_assigned_user_id_status",
                table: "project_tasks",
                columns: new[] { "assigned_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_org_id_project_id_status",
                table: "project_tasks",
                columns: new[] { "org_id", "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_project_id",
                table: "project_tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_time_entries_org_id_task_id_started_at",
                table: "task_time_entries",
                columns: new[] { "org_id", "task_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_time_entries_task_id",
                table: "task_time_entries",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_time_entries_user_id_ended_at",
                table: "task_time_entries",
                columns: new[] { "user_id", "ended_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_activity_sessions_project_tasks_task_id",
                table: "activity_sessions",
                column: "task_id",
                principalTable: "project_tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_activity_sessions_projects_project_id",
                table: "activity_sessions",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_sessions_project_tasks_task_id",
                table: "activity_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_activity_sessions_projects_project_id",
                table: "activity_sessions");

            migrationBuilder.DropTable(
                name: "agent_notification_inbox");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "task_time_entries");

            migrationBuilder.DropTable(
                name: "project_tasks");

            migrationBuilder.DropIndex(
                name: "IX_activity_sessions_project_id",
                table: "activity_sessions");

            migrationBuilder.DropIndex(
                name: "IX_activity_sessions_task_id",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "task_id",
                table: "activity_sessions");
        }
    }
}
