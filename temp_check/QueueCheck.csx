#r "nuget: Microsoft.Data.Sqlite, 9.0.0"
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTrack", "timetrack.db");
Console.WriteLine($"DB Path: {dbPath}");
Console.WriteLine($"Exists: {File.Exists(dbPath)}");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = @"
SELECT id, status, attempt_count,
       CASE WHEN last_error IS NULL THEN 'NULL' ELSE substr(last_error, 1, 120) END,
       datetime(next_attempt_utc),
       datetime('now')
FROM evidence_upload_queue
ORDER BY created_at DESC LIMIT 15";

using var reader = cmd.ExecuteReader();
Console.WriteLine("\n--- Evidence Upload Queue ---");
while (reader.Read())
{
    Console.WriteLine($"{reader.GetString(0).Substring(0,8)}... | {reader.GetString(1),-10} | att={reader.GetInt32(2)} | err={reader.GetString(3)} | next={reader.GetString(4)} | now={reader.GetString(5)}");
}
