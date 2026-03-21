using Microsoft.Data.Sqlite;
using System;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TimeTrack",
    "timetrack.db"
);

Console.WriteLine("========================================");
Console.WriteLine("RESET OUTBOX - Clear backoff for pending items");
Console.WriteLine("========================================");
Console.WriteLine($"Database: {dbPath}");

if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found!");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// Count pending items before reset
var countCmd = connection.CreateCommand();
countCmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NULL";
var pendingBefore = Convert.ToInt32(countCmd.ExecuteScalar());
Console.WriteLine($"Pending items before reset: {pendingBefore}");

if (pendingBefore == 0)
{
    Console.WriteLine("No pending items to reset!");
    return;
}

// Reset next_attempt_utc for all pending items to now
var resetCmd = connection.CreateCommand();
resetCmd.CommandText = @"
    UPDATE sync_outbox
    SET next_attempt_utc = datetime('now'),
        attempt_count = 0,
        last_error = NULL
    WHERE sent_at IS NULL";

var rowsAffected = resetCmd.ExecuteNonQuery();
Console.WriteLine($"Reset {rowsAffected} items - set next_attempt_utc to now");

// Verify
var countAfter = Convert.ToInt32(countCmd.ExecuteScalar());
Console.WriteLine($"Pending items after reset: {countAfter}");

connection.Close();
Console.WriteLine("========================================");
Console.WriteLine("DONE - Restart DesktopHost to sync immediately");
Console.WriteLine("========================================");
