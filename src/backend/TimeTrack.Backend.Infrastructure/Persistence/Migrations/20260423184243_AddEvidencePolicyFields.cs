using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidencePolicyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "evidence_retention_days",
                table: "org_policies",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "screenshot_excluded_apps_json",
                table: "org_policies",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "screenshot_interval_minutes",
                table: "org_policies",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "screenshots_enabled",
                table: "org_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "website_tracking_enabled",
                table: "org_policies",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evidence_retention_days",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshot_excluded_apps_json",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshot_interval_minutes",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshots_enabled",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "website_tracking_enabled",
                table: "org_policies");
        }
    }
}
