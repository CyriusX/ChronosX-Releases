using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds a covering index on (user_id, started_at) for activity_sessions.
    ///
    /// The existing index (org_id, user_id, started_at) cannot be used efficiently
    /// for queries that filter only by user_id — org_id is the leading column so the
    /// DB falls back to a full table scan. All report and activity queries filter by
    /// user_id + date range, making this index critical for load times.
    /// </summary>
    public partial class AddActivitySessionUserIdIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_activity_sessions_user_started
                ON activity_sessions (user_id, started_at DESC);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_activity_sessions_user_started;");
        }
    }
}
