#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Data.Sqlite, 8.0.0"

using Microsoft.Data.Sqlite;
using System;

var dbPath = args.Length > 0 ? args[0] : "timetrack.db";
var connectionString = $"Data Source={dbPath}";

using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("=== IDLE PERIODS ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM idle_periods";
    var count = cmd.ExecuteScalar();
    Console.WriteLine($"Total: {count}");

    cmd.CommandText = "SELECT id, start_utc, end_utc, threshold_seconds FROM idle_periods ORDER BY start_utc DESC LIMIT 5";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - {reader["id"]}: {reader["start_utc"]} -> {reader["end_utc"]} ({reader["threshold_seconds"]}s)");
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
}
