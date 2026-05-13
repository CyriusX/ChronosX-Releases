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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_subscriptions");

            migrationBuilder.DropTable(
                name: "org_usage_records");

            migrationBuilder.DropTable(
                name: "stripe_event_logs");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
