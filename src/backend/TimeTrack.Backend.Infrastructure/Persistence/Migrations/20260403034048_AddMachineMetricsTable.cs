using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineMetricsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_metrics");
        }
    }
}
