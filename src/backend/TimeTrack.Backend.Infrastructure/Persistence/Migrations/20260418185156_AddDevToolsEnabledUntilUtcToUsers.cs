using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDevToolsEnabledUntilUtcToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_tasks_assigned_user_id_status",
                table: "project_tasks");

            migrationBuilder.DropIndex(
                name: "IX_project_tasks_org_id_project_id_status",
                table: "project_tasks");

            migrationBuilder.DropIndex(
                name: "IX_activity_sessions_user_id",
                table: "activity_sessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "devtools_enabled_until_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "connected_at",
                table: "user_integrations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "auth_method",
                table: "user_integrations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<byte[]>(
                name: "refresh_token",
                table: "user_integrations",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "token_expires_at",
                table: "user_integrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "projects",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hourly_rate",
                table: "projects",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_billable",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_task_time_entries_user_id_open",
                table: "task_time_entries",
                column: "user_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_assigned_user_id_status_position",
                table: "project_tasks",
                columns: new[] { "assigned_user_id", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_org_id_project_id_status_position",
                table: "project_tasks",
                columns: new[] { "org_id", "project_id", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_sessions_user_started",
                table: "activity_sessions",
                columns: new[] { "user_id", "started_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_time_entries_user_id_open",
                table: "task_time_entries");

            migrationBuilder.DropIndex(
                name: "IX_project_tasks_assigned_user_id_status_position",
                table: "project_tasks");

            migrationBuilder.DropIndex(
                name: "IX_project_tasks_org_id_project_id_status_position",
                table: "project_tasks");

            migrationBuilder.DropIndex(
                name: "ix_activity_sessions_user_started",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "devtools_enabled_until_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "auth_method",
                table: "user_integrations");

            migrationBuilder.DropColumn(
                name: "refresh_token",
                table: "user_integrations");

            migrationBuilder.DropColumn(
                name: "token_expires_at",
                table: "user_integrations");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "hourly_rate",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_billable",
                table: "projects");

            migrationBuilder.AlterColumn<DateTime>(
                name: "connected_at",
                table: "user_integrations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_assigned_user_id_status",
                table: "project_tasks",
                columns: new[] { "assigned_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_org_id_project_id_status",
                table: "project_tasks",
                columns: new[] { "org_id", "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_user_id",
                table: "activity_sessions",
                column: "user_id");
        }
    }
}
