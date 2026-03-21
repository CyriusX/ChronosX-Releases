using Microsoft.Data.Sqlite;
using System;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TimeTrack",
    "timetrack.db"
);

Console.WriteLine("========================================");
Console.WriteLine("SYNC STATUS CHECKER");
Console.WriteLine("========================================");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"Exists: {File.Exists(dbPath)}");

if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found!");
    return;
}

var fileInfo = new FileInfo(dbPath);
Console.WriteLine($"Size: {fileInfo.Length / 1024 / 1024:F2} MB");
Console.WriteLine($"Last Modified: {fileInfo.LastWriteTime}");

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// Check tables
var tablesCmd = connection.CreateCommand();
tablesCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
Console.WriteLine("\n=== TABLES ===");
using (var reader = tablesCmd.ExecuteReader())
{
    var tables = new List<string>();
    while (reader.Read())
    {
        tables.Add(reader["name"].ToString() ?? "");
    }
    Console.WriteLine(string.Join(", ", tables));
}

// Check outbox status
var outboxCmd = connection.CreateCommand();
outboxCmd.CommandText = @"
    SELECT
        COUNT(*) as total,
        SUM(CASE WHEN sent_at IS NULL THEN 1 ELSE 0 END) as pending,
        SUM(CASE WHEN sent_at IS NOT NULL THEN 1 ELSE 0 END) as sent,
        SUM(CASE WHEN last_error IS NOT NULL THEN 1 ELSE 0 END) as errors
    FROM sync_outbox";
Console.WriteLine("\n=== OUTBOX STATUS ===");
using (var reader = outboxCmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine($"Total items: {reader["total"]}");
        Console.WriteLine($"Pending (not sent): {reader["pending"]}");
        Console.WriteLine($"Sent: {reader["sent"]}");
        Console.WriteLine($"With errors: {reader["errors"]}");
    }
}

// Check pending items with next_attempt_utc in the future (stuck due to backoff)
var futureCmd = connection.CreateCommand();
futureCmd.CommandText = @"
    SELECT COUNT(*) as count
    FROM sync_outbox
    WHERE sent_at IS NULL
      AND next_attempt_utc > datetime('now')";
Console.WriteLine("\n=== ITEMS WITH FUTURE RETRY (stuck due to backoff) ===");
using (var reader = futureCmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine($"Items waiting for future retry: {reader["count"]}");
    }
}

// Check pending items ready to send
var readyCmd = connection.CreateCommand();
readyCmd.CommandText = @"
    SELECT COUNT(*) as count
    FROM sync_outbox
    WHERE sent_at IS NULL
      AND (next_attempt_utc IS NULL OR next_attempt_utc <= datetime('now'))";
Console.WriteLine("\n=== ITEMS READY TO SEND NOW ===");
using (var reader = readyCmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine($"Items ready to send: {reader["count"]}");
    }
}

// Check recent errors
var errorsCmd = connection.CreateCommand();
errorsCmd.CommandText = @"
    SELECT entity_type, SUBSTR(last_error, 1, 150) as error, attempt_count, next_attempt_utc
    FROM sync_outbox
    WHERE last_error IS NOT NULL
    ORDER BY created_at DESC
    LIMIT 5";
Console.WriteLine("\n=== RECENT ERRORS (last 5) ===");
using (var reader = errorsCmd.ExecuteReader())
{
    var hasErrors = false;
    while (reader.Read())
    {
        hasErrors = true;
        Console.WriteLine($"- {reader["entity_type"]}: {reader["error"]}... (attempts: {reader["attempt_count"]}, next: {reader["next_attempt_utc"]})");
    }
    if (!hasErrors) Console.WriteLine("No errors found!");
}

// Check pending items by type
var pendingCmd = connection.CreateCommand();
pendingCmd.CommandText = @"
    SELECT entity_type, COUNT(*) as count
    FROM sync_outbox
    WHERE sent_at IS NULL
    GROUP BY entity_type";
Console.WriteLine("\n=== PENDING ITEMS BY TYPE ===");
using (var reader = pendingCmd.ExecuteReader())
{
    var hasPending = false;
    while (reader.Read())
    {
        hasPending = true;
        Console.WriteLine($"- {reader["entity_type"]}: {reader["count"]} pending");
    }
    if (!hasPending) Console.WriteLine("No pending items!");
}

// Check oldest pending item
var oldestCmd = connection.CreateCommand();
oldestCmd.CommandText = @"
    SELECT entity_type, created_at, attempt_count, next_attempt_utc, sent_at
    FROM sync_outbox
    WHERE sent_at IS NULL
    ORDER BY created_at ASC
    LIMIT 5";
Console.WriteLine("\n=== OLDEST PENDING ITEMS ===");
using (var reader = oldestCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"- Type: {reader["entity_type"]}, Created: {reader["created_at"]}, Attempts: {reader["attempt_count"]}, Next: {reader["next_attempt_utc"]}");
    }
}

// Check activity_sessions table schema
var schemaCmd = connection.CreateCommand();
schemaCmd.CommandText = "PRAGMA table_info(activity_sessions)";
Console.WriteLine("\n=== ACTIVITY_SESSIONS TABLE SCHEMA ===");
using (var reader = schemaCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"  {reader["name"]} ({reader["type"]})");
    }
}

// Check sync_outbox table schema
var outboxSchemaCmd = connection.CreateCommand();
outboxSchemaCmd.CommandText = "PRAGMA table_info(sync_outbox)";
Console.WriteLine("\n=== SYNC_OUTBOX TABLE SCHEMA ===");
using (var reader = outboxSchemaCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"  {reader["name"]} ({reader["type"]})");
    }
}

// Check tracking state
var trackingCmd = connection.CreateCommand();
trackingCmd.CommandText = @"
    SELECT user_id, status, is_paused, last_updated
    FROM tracking_state
    ORDER BY last_updated DESC
    LIMIT 1";
Console.WriteLine("\n=== TRACKING STATE ===");
using (var reader = trackingCmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine($"User: {reader["user_id"]}");
        Console.WriteLine($"Status: {reader["status"]}");
        Console.WriteLine($"Paused: {reader["is_paused"]}");
        Console.WriteLine($"Last Updated: {reader["last_updated"]}");
    }
    else
    {
        Console.WriteLine("No tracking state found!");
    }
}

// Check recent sync_outbox entries (last 10)
var recentCmd = connection.CreateCommand();
recentCmd.CommandText = @"
    SELECT entity_type, created_at, sent_at, last_error, attempt_count, next_attempt_utc
    FROM sync_outbox
    ORDER BY created_at DESC
    LIMIT 10";
Console.WriteLine("\n=== RECENT OUTBOX ENTRIES (last 10) ===");
using (var reader = recentCmd.ExecuteReader())
{
    while (reader.Read())
    {
        var sent = reader["sent_at"] == DBNull.Value ? "PENDING" : "SENT";
        var error = reader["last_error"] == DBNull.Value ? "" : $" ERROR: {reader["last_error"]}";
        Console.WriteLine($"[{sent}] {reader["entity_type"]} - Created: {reader["created_at"]}, Attempts: {reader["attempt_count"]}{error}");
    }
}

connection.Close();
Console.WriteLine("\n========================================");
Console.WriteLine("CHECK COMPLETE");
Console.WriteLine("========================================");
