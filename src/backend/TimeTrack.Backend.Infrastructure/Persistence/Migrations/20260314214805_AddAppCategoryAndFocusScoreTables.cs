using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppCategoryAndFocusScoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================================
            // Add focus_mode_json column to org_policies if not exists
            // ============================================================================
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'org_policies' AND column_name = 'focus_mode_json') THEN
                        ALTER TABLE org_policies ADD COLUMN focus_mode_json jsonb NOT NULL DEFAULT '{}'::jsonb;
                    END IF;
                END $$;
            ");

            // ============================================================================
            // Create app_category_global table if not exists
            // ============================================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS app_category_global (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    identifier TEXT NOT NULL,
                    identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
                    display_name TEXT NOT NULL,
                    productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
                    subcategory TEXT NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    CONSTRAINT uq_app_category_global_identifier UNIQUE (identifier)
                );

                CREATE INDEX IF NOT EXISTS ix_app_category_global_productivity ON app_category_global (productivity);
                CREATE INDEX IF NOT EXISTS ix_app_category_global_display_name ON app_category_global (display_name);
            ");

            // ============================================================================
            // Create app_category_override table if not exists
            // ============================================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS app_category_override (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    org_id UUID NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
                    identifier TEXT NOT NULL,
                    identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
                    display_name TEXT,
                    productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
                    subcategory TEXT NOT NULL,
                    note TEXT,
                    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    CONSTRAINT uq_app_category_override_org_identifier UNIQUE (org_id, identifier)
                );

                CREATE INDEX IF NOT EXISTS ix_app_category_override_org_id ON app_category_override (org_id);
                CREATE INDEX IF NOT EXISTS ix_app_category_override_org_productivity ON app_category_override (org_id, productivity);
                CREATE INDEX IF NOT EXISTS IX_app_category_override_created_by ON app_category_override (created_by);
            ");

            // ============================================================================
            // Create daily_focus_scores table if not exists
            // ============================================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS daily_focus_scores (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    org_id UUID NOT NULL,
                    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
                    date DATE NOT NULL,
                    total_tracked_ms BIGINT NOT NULL DEFAULT 0,
                    focus_time_ms BIGINT NOT NULL DEFAULT 0,
                    distraction_ms BIGINT NOT NULL DEFAULT 0,
                    distraction_count INTEGER NOT NULL DEFAULT 0,
                    pause_count INTEGER NOT NULL DEFAULT 0,
                    idle_count INTEGER NOT NULL DEFAULT 0,
                    long_focus_block_count INTEGER NOT NULL DEFAULT 0,
                    focus_score SMALLINT NOT NULL,
                    calculated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_daily_focus_scores_org_id_user_id_date
                    ON daily_focus_scores (org_id, user_id, date DESC);
                CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_org_id_date
                    ON daily_focus_scores (org_id, date DESC);
                CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_user_id
                    ON daily_focus_scores (user_id);
                CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_device_id
                    ON daily_focus_scores (device_id);
            ");

            // ============================================================================
            // Create daily_summaries table if not exists
            // ============================================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS daily_summaries (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    org_id UUID NOT NULL,
                    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    date TIMESTAMPTZ NOT NULL,
                    total_active_seconds INTEGER NOT NULL,
                    total_idle_seconds INTEGER NOT NULL,
                    session_count INTEGER NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                    updated_at TIMESTAMPTZ
                );

                CREATE INDEX IF NOT EXISTS IX_daily_summaries_org_id_date
                    ON daily_summaries (org_id, date);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_daily_summaries_user_id_date
                    ON daily_summaries (user_id, date);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS daily_summaries;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS daily_focus_scores;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS app_category_override;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS app_category_global;");
            migrationBuilder.Sql(@"ALTER TABLE org_policies DROP COLUMN IF EXISTS focus_mode_json;");
        }
    }
}
