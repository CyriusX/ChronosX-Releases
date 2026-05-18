using System;
using System.IO;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTrack", "timetrack.db");
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

Console.WriteLine("=== Evidence Queue Status ===");
using (var cmd = new SqliteCommand("SELECT status, COUNT(*), MIN(attempt_count), MAX(attempt_count) FROM evidence_upload_queue GROUP BY status", conn))
using (var reader = cmd.ExecuteReader())
    while (reader.Read())
        Console.WriteLine($"  status={reader.GetValue(0)}, count={reader.GetValue(1)}, min_attempts={reader.GetValue(2)}, max_attempts={reader.GetValue(3)}");

Console.WriteLine("\n=== Reset all to pending ===");
using (var resetCmd = new SqliteCommand(@"
    UPDATE evidence_upload_queue
    SET status = 'pending', attempt_count = 0, next_attempt_utc = datetime('now')
    WHERE status != 'pending' OR attempt_count > 0", conn))
{
    var affected = resetCmd.ExecuteNonQuery();
    Console.WriteLine($"  Reset {affected} items");
}

conn.Close();
