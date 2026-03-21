@echo off
:: Simple script to check SQLite database status
:: Run with: dotnet script check_sync_status.csx

#r "nuget: Microsoft.Data.Sqlite, 8.0.0"

using Microsoft.Data.Sqlite;
using System;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TimeTrack",
    "timetrack.db"
);

Console.WriteLine($"Database path: {dbPath}");
Console.WriteLine($"Database exists: {File.Exists(dbPath)}");
Console.WriteLine();

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// Check pending outbox items
using var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT
        COUNT(*) as pending_count,
        COUNT(CASE WHEN sent_at IS NOT NULL THEN 1 END) as sent_count,
        COUNT(CASE WHEN last_error IS NOT NULL THEN 1 END) as error_count
    FROM sync_outbox";

using var reader = cmd.ExecuteReader();
if (reader.Read())
{
    Console.WriteLine($"=== OUTBOX STATUS ===");
    Console.WriteLine($"Pending (not sent): {reader["pending_count"]}");
    Console.WriteLine($"Sent: {reader["sent_count"]}");
    Console.WriteLine($"With errors: {reader["error_count"]}");
}

// Check recent errors
cmd.CommandText = @"
    SELECT entity_type, last_error, attempt_count, next_attempt_utc
    FROM sync_outbox
    WHERE last_error IS NOT NULL
    ORDER BY created_at DESC
    LIMIT 5";

using var errorReader = cmd.ExecuteReader();
Console.WriteLine();
Console.WriteLine("=== RECENT ERRORS ===");
while (errorReader.Read())
{
    Console.WriteLine($"Type: {errorReader["entity_type"]}, Error: {errorReader["last_error"]}, Attempts: {errorReader["attempt_count"]}");
}

// Check activity sessions count by date
cmd.CommandText = @"
    SELECT
        DATE(started_at) as date,
        COUNT(*) as session_count,
        SUM(duration_seconds) as total_seconds
    FROM activity_sessions
    WHERE started_at >= date('now', '-7 days')
    GROUP BY DATE(started_at)
    ORDER BY date DESC";

using var sessionReader = cmd.ExecuteReader();
Console.WriteLine();
Console.WriteLine("=== ACTIVITY SESSIONS (Last 7 Days) ===");
while (sessionReader.Read())
{
    Console.WriteLine($"Date: {sessionReader["date"]}, Sessions: {sessionReader["session_count"]}, Total: {TimeSpan.FromSeconds(Convert.ToInt64(sessionReader["total_seconds"])):hh\\:mm}");
}

// Check today's sessions
cmd.CommandText = @"
    SELECT
        app_display_name,
        SUM(duration_seconds) as total_seconds,
        COUNT(*) as count
    FROM activity_sessions
    WHERE DATE(started_at) = DATE('now')
    GROUP BY app_display_name
    ORDER BY total_seconds DESC
    LIMIT 10";

using var todayReader = cmd.ExecuteReader();
Console.WriteLine();
Console.WriteLine("=== TODAY'S TOP APPS ===");
while (todayReader.Read())
{
    Console.WriteLine($"{todayReader["app_display_name"]}: {TimeSpan.FromSeconds(Convert.ToInt64(todayReader["total_seconds"])):hh\\:mm} ({todayReader["count"]} sessions)");
}

connection.Close();
Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
