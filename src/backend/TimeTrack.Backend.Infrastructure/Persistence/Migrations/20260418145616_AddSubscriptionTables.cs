using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_category_global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    productivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subcategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_category_global", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.id);
                });

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
                name: "org_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    work_hours_json = table.Column<string>(type: "jsonb", nullable: false),
                    app_exclusions_json = table.Column<string>(type: "jsonb", nullable: false),
                    focus_mode_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    idle_threshold_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 180),
                    retention_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_usage_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_users_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    active_devices_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_usage_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orgs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    org_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orgs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stripe_event_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_event_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stripe_price_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stripe_product_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monthly_price_cents = table.Column<int>(type: "integer", nullable: false),
                    yearly_price_cents = table.Column<int>(type: "integer", nullable: true),
                    max_users = table.Column<int>(type: "integer", nullable: false),
                    max_devices = table.Column<int>(type: "integer", nullable: false),
                    machine_monitoring = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    advanced_reports = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    focus_mode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    api_access = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    priority_support = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    custom_categories = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    linear_integration = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    billing_analytics = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_policies_orgs_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "orgs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValue: "#4A9FFF"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    hourly_rate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    sync_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Local"),
                    linear_project_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    linear_workspace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.ForeignKey(
                        name: "FK_projects_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    password_must_change = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stripe_customer_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stripe_subscription_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    grace_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_subscriptions_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "app_category_override",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    productivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subcategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_category_override", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_category_override_orgs_org_id",
                        column: x => x.org_id,
                        principalTable: "orgs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_category_override_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_active_seconds = table.Column<int>(type: "integer", nullable: false),
                    total_idle_seconds = table.Column<int>(type: "integer", nullable: false),
                    session_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_summaries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    agent_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    os_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    uptime_seconds = table.Column<int>(type: "integer", nullable: true),
                    tracking_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    health_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consecutive_sync_failures = table.Column<int>(type: "integer", nullable: true),
                    last_successful_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipc_connected = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_devices_orgs_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "orgs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
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
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    linear_issue_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    linear_issue_identifier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    linear_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    linear_state_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    linear_state_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    linear_team_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
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
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    refresh_token = table.Column<byte[]>(type: "bytea", nullable: true),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auth_method = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "agent_event_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    metadata_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_event_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_event_logs_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_focus_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_tracked_ms = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    focus_time_ms = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    distraction_ms = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    distraction_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pause_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    idle_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    long_focus_block_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    focus_score = table.Column<short>(type: "smallint", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_focus_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_focus_scores_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_daily_focus_scores_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "focus_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    planned_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    actual_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    focus_score = table.Column<int>(type: "integer", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_focus_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_focus_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_focus_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "idle_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idle_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_idle_periods_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_idle_periods_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpu_percent = table.Column<double>(type: "double precision", nullable: false),
                    memory_used_mb = table.Column<long>(type: "bigint", nullable: false),
                    memory_total_mb = table.Column<long>(type: "bigint", nullable: false),
                    disk_used_gb = table.Column<double>(type: "double precision", nullable: false),
                    disk_total_gb = table.Column<double>(type: "double precision", nullable: false),
                    sampled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_metrics_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "remote_commands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remote_commands", x => x.id);
                    table.ForeignKey(
                        name: "FK_remote_commands_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    window_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    app_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    app_subcategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_sessions_project_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "project_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_activity_sessions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_activity_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_activity_sessions_device_id_started_at",
                table: "activity_sessions",
                columns: new[] { "device_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_idempotency_key",
                table: "activity_sessions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_org_id_user_id_started_at",
                table: "activity_sessions",
                columns: new[] { "org_id", "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_project_id",
                table: "activity_sessions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_task_id",
                table: "activity_sessions",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_sessions_user_started",
                table: "activity_sessions",
                columns: new[] { "user_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_agent_event_logs_category_type",
                table: "agent_event_logs",
                columns: new[] { "category", "event_type" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_event_logs_device_timestamp",
                table: "agent_event_logs",
                columns: new[] { "device_id", "timestamp_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_agent_event_logs_org_id",
                table: "agent_event_logs",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_event_logs_severity",
                table: "agent_event_logs",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_agent_event_logs_timestamp",
                table: "agent_event_logs",
                column: "timestamp_utc");

            migrationBuilder.CreateIndex(
                name: "IX_agent_notification_inbox_org_id_user_id_created_at",
                table: "agent_notification_inbox",
                columns: new[] { "org_id", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_notification_inbox_user_id_read_at",
                table: "agent_notification_inbox",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_app_category_global_display_name",
                table: "app_category_global",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_app_category_global_productivity",
                table: "app_category_global",
                column: "productivity");

            migrationBuilder.CreateIndex(
                name: "uq_app_category_global_identifier",
                table: "app_category_global",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_category_override_created_by",
                table: "app_category_override",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_app_category_override_org_id",
                table: "app_category_override",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_category_override_org_productivity",
                table: "app_category_override",
                columns: new[] { "org_id", "productivity" });

            migrationBuilder.CreateIndex(
                name: "uq_app_category_override_org_identifier",
                table: "app_category_override",
                columns: new[] { "org_id", "identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_org_id_created_at",
                table: "audit_log",
                columns: new[] { "org_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_user_id_created_at",
                table: "audit_log",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_focus_scores_device_id",
                table: "daily_focus_scores",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_focus_scores_org_id_date",
                table: "daily_focus_scores",
                columns: new[] { "org_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_daily_focus_scores_org_id_user_id_date",
                table: "daily_focus_scores",
                columns: new[] { "org_id", "user_id", "date" },
                unique: true,
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_daily_focus_scores_user_id",
                table: "daily_focus_scores",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_summaries_org_id_date",
                table: "daily_summaries",
                columns: new[] { "org_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_summaries_user_id_date",
                table: "daily_summaries",
                columns: new[] { "user_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_org_id_user_id",
                table: "devices",
                columns: new[] { "org_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_OrganizationId",
                table: "devices",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_user_id",
                table: "devices",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_device_id_started_at",
                table: "focus_sessions",
                columns: new[] { "device_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_idempotency_key",
                table: "focus_sessions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_org_id_user_id_started_at",
                table: "focus_sessions",
                columns: new[] { "org_id", "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_focus_sessions_user_id",
                table: "focus_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_expires_at",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_key",
                table: "idempotency_keys",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idle_periods_device_id_started_at",
                table: "idle_periods",
                columns: new[] { "device_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_idle_periods_idempotency_key",
                table: "idle_periods",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idle_periods_org_id_user_id_started_at",
                table: "idle_periods",
                columns: new[] { "org_id", "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_idle_periods_user_id",
                table: "idle_periods",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_linear_sync_history_user_id_started_at",
                table: "linear_sync_history",
                columns: new[] { "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_machine_metrics_device_sampled",
                table: "machine_metrics",
                columns: new[] { "device_id", "sampled_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_machine_metrics_org_id",
                table: "machine_metrics",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_metrics_sampled_at",
                table: "machine_metrics",
                column: "sampled_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_org_policies_org_id",
                table: "org_policies",
                column: "org_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_policies_version",
                table: "org_policies",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "IX_org_subscriptions_org_id",
                table: "org_subscriptions",
                column: "org_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_subscriptions_plan_id",
                table: "org_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_subscriptions_stripe_customer_id",
                table: "org_subscriptions",
                column: "stripe_customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_subscriptions_stripe_subscription_id",
                table: "org_subscriptions",
                column: "stripe_subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_usage_records_org_id",
                table: "org_usage_records",
                column: "org_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orgs_slug",
                table: "orgs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_policies_org_id_type",
                table: "policies",
                columns: new[] { "org_id", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_policies_OrganizationId",
                table: "policies",
                column: "OrganizationId");

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
                name: "IX_project_tasks_assigned_user_id_status_position",
                table: "project_tasks",
                columns: new[] { "assigned_user_id", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_org_id_linear_issue_id",
                table: "project_tasks",
                columns: new[] { "org_id", "linear_issue_id" },
                unique: true,
                filter: "linear_issue_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_org_id_project_id_status_position",
                table: "project_tasks",
                columns: new[] { "org_id", "project_id", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_project_id",
                table: "project_tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_org_id_linear_project_id",
                table: "projects",
                columns: new[] { "org_id", "linear_project_id" },
                unique: true,
                filter: "linear_project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_projects_org_id_name",
                table: "projects",
                columns: new[] { "org_id", "name" },
                unique: true,
                filter: "sync_source = 'Local'");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_device_id",
                table: "refresh_tokens",
                columns: new[] { "user_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_remote_commands_device_status",
                table: "remote_commands",
                columns: new[] { "device_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_remote_commands_org_device_created",
                table: "remote_commands",
                columns: new[] { "org_id", "device_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_stripe_event_logs_org_id_processed_at",
                table: "stripe_event_logs",
                columns: new[] { "org_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stripe_event_logs_stripe_event_id",
                table: "stripe_event_logs",
                column: "stripe_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_tier",
                table: "subscription_plans",
                column: "tier",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_task_time_entries_user_id_open",
                table: "task_time_entries",
                column: "user_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_integrations_user_id_provider",
                table: "user_integrations",
                columns: new[] { "user_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_org_id_email",
                table: "users",
                columns: new[] { "org_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_sessions");

            migrationBuilder.DropTable(
                name: "agent_event_logs");

            migrationBuilder.DropTable(
                name: "agent_notification_inbox");

            migrationBuilder.DropTable(
                name: "app_category_global");

            migrationBuilder.DropTable(
                name: "app_category_override");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "daily_focus_scores");

            migrationBuilder.DropTable(
                name: "daily_summaries");

            migrationBuilder.DropTable(
                name: "focus_sessions");

            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "idle_periods");

            migrationBuilder.DropTable(
                name: "linear_sync_history");

            migrationBuilder.DropTable(
                name: "machine_metrics");

            migrationBuilder.DropTable(
                name: "org_policies");

            migrationBuilder.DropTable(
                name: "org_subscriptions");

            migrationBuilder.DropTable(
                name: "org_usage_records");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "remote_commands");

            migrationBuilder.DropTable(
                name: "stripe_event_logs");

            migrationBuilder.DropTable(
                name: "task_time_entries");

            migrationBuilder.DropTable(
                name: "user_integrations");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "project_tasks");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "orgs");
        }
    }
}
