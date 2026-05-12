using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReportSchedulesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "avg_focus_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "browser_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "context_switches_count",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "distinct_apps_count",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "distraction_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "focus_sessions_completed",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "focus_sessions_count",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "interruption_count",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "longest_focus_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "neutral_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "productive_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "productivity_ratio",
                table: "daily_focus_scores",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "top_app_exe",
                table: "daily_focus_scores",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "top_app_seconds",
                table: "daily_focus_scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ai_decision_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_data = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    output = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    was_reviewed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    review_outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    correct_value = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_decision_log", x => x.id);
                });

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
                    evidence = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    baseline_value = table.Column<double>(type: "double precision", nullable: true),
                    actual_value = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_behavioral_anomalies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feature_monthly",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month_start = table.Column<DateOnly>(type: "date", nullable: false),
                    avg_focus_score = table.Column<double>(type: "double precision", nullable: false),
                    avg_productive_ratio = table.Column<double>(type: "double precision", nullable: false),
                    total_active_hours = table.Column<double>(type: "double precision", nullable: false),
                    trend_focus_score = table.Column<double>(type: "double precision", nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_monthly", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_monthly_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_weekly",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    avg_focus_score = table.Column<double>(type: "double precision", nullable: false),
                    avg_productive_ratio = table.Column<double>(type: "double precision", nullable: false),
                    total_active_hours = table.Column<double>(type: "double precision", nullable: false),
                    avg_context_switches = table.Column<double>(type: "double precision", nullable: false),
                    avg_interruption_count = table.Column<double>(type: "double precision", nullable: false),
                    trend_focus_score = table.Column<double>(type: "double precision", nullable: true),
                    trend_productive_ratio = table.Column<double>(type: "double precision", nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_weekly", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_weekly_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smart_alerts", x => x.id);
                });

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
                    evidence = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_patterns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "weekly_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    time_of_day = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    preferences_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_report_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decision_log_org_id_decision_type_created_at",
                table: "ai_decision_log",
                columns: new[] { "org_id", "decision_type", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decision_log_user_id_decision_type_created_at",
                table: "ai_decision_log",
                columns: new[] { "user_id", "decision_type", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decision_log_was_reviewed_decision_type",
                table: "ai_decision_log",
                columns: new[] { "was_reviewed", "decision_type" },
                filter: "was_reviewed = false");

            migrationBuilder.CreateIndex(
                name: "IX_behavioral_anomalies_org_id_severity_detected_at",
                table: "behavioral_anomalies",
                columns: new[] { "org_id", "severity", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_behavioral_anomalies_user_id_detected_at",
                table: "behavioral_anomalies",
                columns: new[] { "user_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_monthly_org_id_month_start",
                table: "feature_monthly",
                columns: new[] { "org_id", "month_start" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_monthly_user_id_month_start",
                table: "feature_monthly",
                columns: new[] { "user_id", "month_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_weekly_org_id_week_start",
                table: "feature_weekly",
                columns: new[] { "org_id", "week_start" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_weekly_user_id_week_start",
                table: "feature_weekly",
                columns: new[] { "user_id", "week_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_smart_alerts_alert_type_user_id_created_at",
                table: "smart_alerts",
                columns: new[] { "alert_type", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_smart_alerts_org_id_severity_created_at",
                table: "smart_alerts",
                columns: new[] { "org_id", "severity", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_smart_alerts_user_id_was_read_created_at",
                table: "smart_alerts",
                columns: new[] { "user_id", "was_read", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_patterns_user_id_is_active_detected_at",
                table: "user_patterns",
                columns: new[] { "user_id", "is_active", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_patterns_user_id_pattern_tag_detected_at",
                table: "user_patterns",
                columns: new[] { "user_id", "pattern_tag", "detected_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_report_schedules_is_enabled_day_of_week",
                table: "weekly_report_schedules",
                columns: new[] { "is_enabled", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_report_schedules_user_id",
                table: "weekly_report_schedules",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_decision_log");

            migrationBuilder.DropTable(
                name: "behavioral_anomalies");

            migrationBuilder.DropTable(
                name: "feature_monthly");

            migrationBuilder.DropTable(
                name: "feature_weekly");

            migrationBuilder.DropTable(
                name: "smart_alerts");

            migrationBuilder.DropTable(
                name: "user_patterns");

            migrationBuilder.DropTable(
                name: "weekly_report_schedules");

            migrationBuilder.DropColumn(
                name: "avg_focus_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "browser_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "context_switches_count",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "distinct_apps_count",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "distraction_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "focus_sessions_completed",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "focus_sessions_count",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "interruption_count",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "longest_focus_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "neutral_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "productive_seconds",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "productivity_ratio",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "top_app_exe",
                table: "daily_focus_scores");

            migrationBuilder.DropColumn(
                name: "top_app_seconds",
                table: "daily_focus_scores");
        }
    }
}
