using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    public partial class AddBillableProjectFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS is_billable boolean NOT NULL DEFAULT false;");
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS currency character varying(3);");
            migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN IF NOT EXISTS hourly_rate decimal(18,2);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "hourly_rate", table: "projects");
            migrationBuilder.DropColumn(name: "currency", table: "projects");
            migrationBuilder.DropColumn(name: "is_billable", table: "projects");
        }
    }
}
