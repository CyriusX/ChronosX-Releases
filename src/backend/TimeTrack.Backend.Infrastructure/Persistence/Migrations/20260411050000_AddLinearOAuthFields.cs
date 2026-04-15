using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    public partial class AddLinearOAuthFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS refresh_token bytea;");
            migrationBuilder.Sql("ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS token_expires_at timestamp with time zone;");
            migrationBuilder.Sql("ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS auth_method integer NOT NULL DEFAULT 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "token_expires_at", table: "user_integrations");
            migrationBuilder.DropColumn(name: "refresh_token", table: "user_integrations");
            migrationBuilder.DropColumn(name: "auth_method", table: "user_integrations");
        }
    }
}
