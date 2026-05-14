using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Combined migration to catch up all missing changes from migrations 25-33
    /// This is needed because the deployed container has code that stops at migration 24
    /// </summary>
    public partial class CatchupMissingMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // From 20260423184243_AddEvidencePolicyFields
            migrationBuilder.AddColumn<bool>(
                name: "RequireScreenshots",
                table: "org_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotFrequencyMinutes",
                table: "org_policies",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceRetentionEnabled",
                table: "org_policies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceRetentionDays",
                table: "org_policies",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            // From 20260423190525_AddEvidenceItemsAndStorageKeys
            migrationBuilder.CreateTable(
                name: "evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_time_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    thumbnail_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    captured_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_items_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_org_id",
                table: "evidence_items",
                column: "OrgId");

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "time_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceScreenshotKey",
                table: "time_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // From 20260423201010_AddStorageQuotaFields
            migrationBuilder.AddColumn<long>(
                name: "StorageQuotaBytes",
                table: "orgs",
                type: "bigint",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "StorageUsedBytes",
                table: "orgs",
                type: "bigint",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceItemCount",
                table: "orgs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // From 20260425130000_AddIdleJustificationSupport
            migrationBuilder.AddColumn<string>(
                name: "IdleJustification",
                table: "time_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManuallyEdited",
                table: "time_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // From 20260425154636_AddAiModulePhase1
            migrationBuilder.CreateTable(
                name: "team_insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    insight_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    summary_text = table.Column<string>(type: "text", nullable: true),
                    top_distractions = table.Column<string>(type: "jsonb", nullable: true),
                    productivity_trend = table.Column<string>(type: "jsonb", nullable: true),
                    team_focus_score = table.Column<decimal>(type: "numeric", nullable: true),
                    total_hours = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    active_user_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_insights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_insights_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deep_work_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    focus_score_avg = table.Column<decimal>(type: "numeric", nullable: true),
                    context_switches = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    distraction_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    most_productive_hour = table.Column<int>(type: "integer", nullable: true),
                    top_apps = table.Column<string>(type: "jsonb", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_insights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_insights_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_insights_users_user_id",
                        column: x => x.UserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "narrative_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_narrative_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_narrative_reports_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_narrative_reports_users_user_id",
                        column: x => x.UserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "classification_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_items = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processed_items = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_jobs", x => x.Id);
                });

            // From 20260425170000_AddPatternAndAnomalyTables
            migrationBuilder.CreateTable(
                name: "productivity_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    first_detected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_detected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    detection_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_stale = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productivity_patterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_productivity_patterns_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_productivity_patterns_users_user_id",
                        column: x => x.UserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "anomaly_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anomaly_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    detected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    context_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anomaly_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_anomaly_events_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_anomaly_events_users_user_id",
                        column: x => x.UserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });

            // From 20260425190000_AddSmartAlertsTable
            migrationBuilder.CreateTable(
                name: "smart_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    affected_users = table.Column<string>(type: "jsonb", nullable: true),
                    triggered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    acknowledged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smart_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_smart_alerts_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                });

            // From 20260507000000_AddOrgInviteLinksAndOnboarding
            migrationBuilder.CreateTable(
                name: "org_invite_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: true),
                    uses_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_invite_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_invite_links_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_invite_links_users_created_by_user_id",
                        column: x => x.CreatedByUserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingCompleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // From 20260512133429_AddWeeklyReportSchedulesTable
            migrationBuilder.CreateTable(
                name: "weekly_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    hour_utc = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_report_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weekly_report_schedules_orgs_org_id",
                        column: x => x.OrgId,
                        principalColumn: "id",
                        principalTable: "orgs",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_weekly_report_schedules_users_user_id",
                        column: x => x.UserId,
                        principalColumn: "id",
                        principalTable: "users",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse all changes in reverse order
            migrationBuilder.DropTable(
                name: "weekly_report_schedules");

            migrationBuilder.DropTable(
                name: "org_invite_links");

            migrationBuilder.DropTable(
                name: "smart_alerts");

            migrationBuilder.DropTable(
                name: "anomaly_events");

            migrationBuilder.DropTable(
                name: "productivity_patterns");

            migrationBuilder.DropTable(
                name: "classification_jobs");

            migrationBuilder.DropTable(
                name: "narrative_reports");

            migrationBuilder.DropTable(
                name: "user_insights");

            migrationBuilder.DropTable(
                name: "team_insights");

            migrationBuilder.DropTable(
                name: "evidence_items");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OnboardingCompleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "StorageQuotaBytes",
                table: "orgs");

            migrationBuilder.DropColumn(
                name: "StorageUsedBytes",
                table: "orgs");

            migrationBuilder.DropColumn(
                name: "EvidenceItemCount",
                table: "orgs");

            migrationBuilder.DropColumn(
                name: "ManuallyEdited",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "IdleJustification",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "EvidenceScreenshotKey",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "ScreenshotFrequencyMinutes",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "RequireScreenshots",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "EvidenceRetentionDays",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "EvidenceRetentionEnabled",
                table: "org_policies");
        }
    }
}
