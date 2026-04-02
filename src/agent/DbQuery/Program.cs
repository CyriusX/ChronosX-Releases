using Microsoft.Data.Sqlite;
using System;

var dbPath = args.Length > 0 ? args[0] : "../TimeTrack.AgentService/timetrack.db";
var connectionString = $"Data Source={dbPath}";
var shouldCleanup = args.Length > 1 && args[1].Equals("cleanup", StringComparison.OrdinalIgnoreCase);
var sqlMode = args.Length > 1 && args[1].Equals("sql", StringComparison.OrdinalIgnoreCase);

using var connection = new SqliteConnection(connectionString);
connection.Open();

// SQL execution mode: dotnet run -- <dbPath> sql "SELECT/DELETE/UPDATE ..."
if (sqlMode && args.Length > 2)
{
    var sql = args[2];
    using var cmd = connection.CreateCommand();
    cmd.CommandText = sql;
    if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
    {
        using var reader = cmd.ExecuteReader();
        var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToArray();
        Console.WriteLine(string.Join(" | ", cols));
        Console.WriteLine(new string('-', cols.Length * 20));
        while (reader.Read())
        {
            var vals = Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()).ToArray();
            Console.WriteLine(string.Join(" | ", vals!));
        }
    }
    else
    {
        var affected = cmd.ExecuteNonQuery();
        Console.WriteLine($"Executed: {affected} row(s) affected");
    }
    return;
}

// Cleanup mode
if (shouldCleanup)
{
    var cutoffDate = DateTime.Today.ToUniversalTime();
    Console.WriteLine($"=== CLEANUP OLD DATA (before {cutoffDate:yyyy-MM-dd}) ===");

    using var transaction = connection.BeginTransaction();
    try
    {
        // Delete old activity sessions
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM activity_sessions WHERE date(start_utc) < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoffDate.ToString("yyyy-MM-dd"));
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted activity_sessions: {deleted}");
        }

        // Delete orphaned outbox items
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                DELETE FROM sync_outbox
                WHERE entity_type = 'activity_session'
                  AND entity_id NOT IN (SELECT id FROM activity_sessions)";
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted orphaned outbox items: {deleted}");
        }

        // Delete old idle periods
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM idle_periods WHERE date(start_utc) < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoffDate.ToString("yyyy-MM-dd"));
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted idle_periods: {deleted}");
        }

        transaction.Commit();
        Console.WriteLine("\n=== CLEANUP COMPLETED ===");
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        Console.WriteLine($"ERROR: {ex.Message}");
    }

    // Show remaining
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM activity_sessions";
        Console.WriteLine($"\nRemaining activity_sessions: {cmd.ExecuteScalar()}");
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NULL";
        Console.WriteLine($"Remaining pending outbox: {cmd.ExecuteScalar()}");
    }
    return;
}

Console.WriteLine("=== IDLE PERIODS ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM idle_periods";
    var count = cmd.ExecuteScalar();
    Console.WriteLine($"Total: {count}");

    cmd.CommandText = "SELECT id, user_id, start_utc, end_utc, threshold_seconds FROM idle_periods ORDER BY start_utc DESC LIMIT 5";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - {reader["id"]}: user={reader["user_id"]}, {reader["start_utc"]} -> {reader["end_utc"]} ({reader["threshold_seconds"]}s)");
    }
}

Console.WriteLine("\n=== SYNC OUTBOX (Pending) ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NULL";
    var pending = cmd.ExecuteScalar();
    Console.WriteLine($"Pending items: {pending}");

    cmd.CommandText = "SELECT id, entity_type, entity_id, next_attempt_utc, last_error FROM sync_outbox WHERE sent_at IS NULL ORDER BY next_attempt_utc LIMIT 10";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - [{reader["entity_type"]}] {reader["entity_id"]}: next={reader["next_attempt_utc"]}, error={reader["last_error"]}");
    }
}

Console.WriteLine("\n=== SYNC OUTBOX (Sent) ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NOT NULL";
    var sent = cmd.ExecuteScalar();
    Console.WriteLine($"Sent items: {sent}");

    cmd.CommandText = "SELECT id, entity_type, sent_at FROM sync_outbox WHERE sent_at IS NOT NULL ORDER BY sent_at DESC LIMIT 5";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - [{reader["entity_type"]}] sent at {reader["sent_at"]}");
    }
}

Console.WriteLine("\n=== SYNC ERRORS ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM sync_errors";
    var errors = cmd.ExecuteScalar();
    Console.WriteLine($"Total errors: {errors}");

    cmd.CommandText = "SELECT endpoint, status_code, error_message, timestamp_utc FROM sync_errors ORDER BY timestamp_utc DESC LIMIT 5";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - [{reader["status_code"]}] {reader["endpoint"]}: {reader["error_message"]} at {reader["timestamp_utc"]}");
    }
}

Console.WriteLine("\n=== ACTIVITY SESSIONS ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM activity_sessions";
    var count = cmd.ExecuteScalar();
    Console.WriteLine($"Total: {count}");

    cmd.CommandText = @"
        SELECT id, display_name, start_utc, end_utc,
               CAST((julianday(end_utc) - julianday(start_utc)) * 86400 AS INTEGER) as duration_seconds
        FROM activity_sessions
        ORDER BY start_utc DESC
        LIMIT 10";
    using var reader = cmd.ExecuteReader();
    Console.WriteLine("\n  Most recent sessions:");
    while (reader.Read())
    {
        Console.WriteLine($"  - {reader["display_name"]}: {reader["start_utc"]} -> {reader["end_utc"]} ({reader["duration_seconds"]}s)");
    }
}
