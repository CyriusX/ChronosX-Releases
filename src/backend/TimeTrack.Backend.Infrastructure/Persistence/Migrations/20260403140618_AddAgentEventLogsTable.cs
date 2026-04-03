using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEventLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_event_logs");
        }
    }
}
