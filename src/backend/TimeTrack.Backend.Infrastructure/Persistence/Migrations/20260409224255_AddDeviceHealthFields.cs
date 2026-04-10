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
            migrationBuilder.AddColumn<int>(
                name: "consecutive_sync_failures",
                table: "devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "health_status",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ipc_connected",
                table: "devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_successful_sync_at",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);
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
