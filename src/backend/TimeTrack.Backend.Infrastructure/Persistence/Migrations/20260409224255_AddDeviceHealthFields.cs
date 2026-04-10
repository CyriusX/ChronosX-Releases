using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHealthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS to be safe — columns may already exist if a previous
            // deployment applied them manually before the migration history was recorded.
            migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS consecutive_sync_failures integer;");
            migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS health_status character varying(20);");
            migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS ipc_connected boolean;");
            migrationBuilder.Sql("ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_successful_sync_at timestamp with time zone;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consecutive_sync_failures",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "health_status",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "ipc_connected",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "last_successful_sync_at",
                table: "devices");
        }
    }
}
