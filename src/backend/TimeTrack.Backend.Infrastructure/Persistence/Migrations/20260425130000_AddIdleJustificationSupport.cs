using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdleJustificationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE org_policies
                ADD COLUMN IF NOT EXISTS idle_justification_prompt_threshold_seconds integer NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                ADD COLUMN IF NOT EXISTS justification_reason_code character varying(64) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                ADD COLUMN IF NOT EXISTS justification_note character varying(500) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                ADD COLUMN IF NOT EXISTS justification_submitted_at_utc timestamptz NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                DROP COLUMN IF EXISTS justification_submitted_at_utc;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                DROP COLUMN IF EXISTS justification_note;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE idle_periods
                DROP COLUMN IF EXISTS justification_reason_code;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE org_policies
                DROP COLUMN IF EXISTS idle_justification_prompt_threshold_seconds;
            ");
        }
    }
}
