using Microsoft.EntityFrameworkCore.Migrations;

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations;

public partial class AddSmartAlertsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "smart_alerts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                about_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                alert_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                message = table.Column<string>(type: "text", nullable: false),
                severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                was_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                was_acted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_smart_alerts", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_smart_alerts_user_id_was_read_created_at",
            table: "smart_alerts",
            columns: new[] { "user_id", "was_read", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_smart_alerts_org_id_severity_created_at",
            table: "smart_alerts",
            columns: new[] { "org_id", "severity", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("smart_alerts");
    }
}
