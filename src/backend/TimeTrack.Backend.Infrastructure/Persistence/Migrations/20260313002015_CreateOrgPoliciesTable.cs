using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateOrgPoliciesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    work_hours_json = table.Column<string>(type: "jsonb", nullable: false),
                    app_exclusions_json = table.Column<string>(type: "jsonb", nullable: false),
                    idle_threshold_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 180),
                    retention_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_policies_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_org_policies_org_id",
                table: "org_policies",
                column: "org_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_policies_version",
                table: "org_policies",
                column: "version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_policies");
        }
    }
}
