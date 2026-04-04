using Npgsql;
using System;
using System.Threading.Tasks;

class MigrationRunner
{
    static async Task Main()
    {
        var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

        var sql = @"
CREATE TABLE IF NOT EXISTS daily_focus_scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
    date DATE NOT NULL,
    total_tracked_ms BIGINT NOT NULL DEFAULT 0,
    focus_time_ms BIGINT NOT NULL DEFAULT 0,
    distraction_ms BIGINT NOT NULL DEFAULT 0,
    distraction_count INT NOT NULL DEFAULT 0,
    pause_count INT NOT NULL DEFAULT 0,
    idle_count INT NOT NULL DEFAULT 0,
    focus_score SMALLINT NOT NULL CHECK (focus_score BETWEEN 0 AND 100),
    calculated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_daily_focus_scores_org_user_date UNIQUE (org_id, user_id, date)
);

CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_org_user_date ON daily_focus_scores (org_id, user_id, date DESC);
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_org_date ON daily_focus_scores (org_id, date DESC);
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_user_id ON daily_focus_scores (user_id);
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_device_id ON daily_focus_scores (device_id);
";

        Console.WriteLine("Connecting to database...");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Console.WriteLine("Connected! Executing migration...");
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Migration executed successfully!");
    }
}
