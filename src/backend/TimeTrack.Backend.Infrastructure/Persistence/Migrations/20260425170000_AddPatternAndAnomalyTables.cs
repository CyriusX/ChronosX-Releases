using Microsoft.EntityFrameworkCore.Migrations;

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations;

public partial class AddPatternAndAnomalyTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_patterns",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                pattern_tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                detected_at = table.Column<DateOnly>(type: "date", nullable: false),
                strength = table.Column<double>(type: "double precision", nullable: false),
                evidence = table.Column<string>(type: "jsonb", nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_patterns", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_patterns_user_id_pattern_tag_detected_at",
            table: "user_patterns",
            columns: new[] { "user_id", "pattern_tag", "detected_at" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_patterns_user_id_is_active_detected_at",
            table: "user_patterns",
            columns: new[] { "user_id", "is_active", "detected_at" });

        migrationBuilder.CreateTable(
            name: "behavioral_anomalies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_id = table.Column<Guid>(type: "uuid", nullable: false),
                anomaly_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                detected_at = table.Column<DateOnly>(type: "date", nullable: false),
                evidence = table.Column<string>(type: "jsonb", nullable: false),
                baseline_value = table.Column<double>(type: "double precision", nullable: true),
                actual_value = table.Column<double>(type: "double precision", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_behavioral_anomalies", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_behavioral_anomalies_user_id_detected_at",
            table: "behavioral_anomalies",
            columns: new[] { "user_id", "detected_at" });

        migrationBuilder.CreateIndex(
            name: "IX_behavioral_anomalies_org_id_severity_detected_at",
            table: "behavioral_anomalies",
            columns: new[] { "org_id", "severity", "detected_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "behavioral_anomalies");
        migrationBuilder.DropTable(name: "user_patterns");
    }
}
