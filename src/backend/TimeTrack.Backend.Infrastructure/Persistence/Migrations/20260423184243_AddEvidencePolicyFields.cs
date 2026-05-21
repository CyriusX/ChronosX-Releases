using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidencePolicyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if columns already exist before adding (idempotent migration)
            // This handles cases where columns were added manually in production

            // evidence_retention_days
            migrationBuilder.Sql(
                "DO $$ " +
                "BEGIN " +
                "    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'org_policies' AND column_name = 'evidence_retention_days') THEN " +
                "        ALTER TABLE org_policies ADD COLUMN evidence_retention_days integer NOT NULL DEFAULT 30; " +
                "    END IF; " +
                "END $$;");

            // screenshot_excluded_apps_json
            migrationBuilder.Sql(
                "DO $$ " +
                "BEGIN " +
                "    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'org_policies' AND column_name = 'screenshot_excluded_apps_json') THEN " +
                "        ALTER TABLE org_policies ADD COLUMN screenshot_excluded_apps_json jsonb NOT NULL DEFAULT '[]'::jsonb; " +
                "    END IF; " +
                "END $$;");

            // screenshot_interval_minutes
            migrationBuilder.Sql(
                "DO $$ " +
                "BEGIN " +
                "    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'org_policies' AND column_name = 'screenshot_interval_minutes') THEN " +
                "        ALTER TABLE org_policies ADD COLUMN screenshot_interval_minutes integer NOT NULL DEFAULT 5; " +
                "    END IF; " +
                "END $$;");

            // screenshots_enabled
            migrationBuilder.Sql(
                "DO $$ " +
                "BEGIN " +
                "    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'org_policies' AND column_name = 'screenshots_enabled') THEN " +
                "        ALTER TABLE org_policies ADD COLUMN screenshots_enabled boolean NOT NULL DEFAULT false; " +
                "    END IF; " +
                "END $$;");

            // website_tracking_enabled
            migrationBuilder.Sql(
                "DO $$ " +
                "BEGIN " +
                "    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'org_policies' AND column_name = 'website_tracking_enabled') THEN " +
                "        ALTER TABLE org_policies ADD COLUMN website_tracking_enabled boolean NOT NULL DEFAULT true; " +
                "    END IF; " +
                "END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evidence_retention_days",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshot_excluded_apps_json",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshot_interval_minutes",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "screenshots_enabled",
                table: "org_policies");

            migrationBuilder.DropColumn(
                name: "website_tracking_enabled",
                table: "org_policies");
        }
    }
}
