using Microsoft.Data.Sqlite;
var dbPath = @"C:\Users\Usuario\AppData\Local\TimeTrack\timetrack.db";
await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

Console.WriteLine("=== Outbox State ===");
await Q(conn, @"
    SELECT
        CASE WHEN sent_at IS NOT NULL THEN 'sent'
             WHEN last_error IS NOT NULL THEN 'error'
             ELSE 'pending' END as status,
        COUNT(*) as cnt
    FROM sync_outbox
    GROUP BY status");

Console.WriteLine("\n=== Latest Events (last 10) ===");
await Q(conn, "SELECT event_type, message, timestamp_utc FROM agent_event_log ORDER BY timestamp_utc DESC LIMIT 10");

Console.WriteLine("\n=== Sent Count ===");
await Q(conn, "SELECT COUNT(*) as sent_count FROM sync_outbox WHERE sent_at IS NOT NULL");

async Task Q(SqliteConnection c, string sql) {
    try {
        await using var cmd = new SqliteCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        var cols = Enumerable.Range(0, r.FieldCount).Select(i => r.GetName(i)).ToArray();
        Console.WriteLine(string.Join(" | ", cols));
        Console.WriteLine(new string('-', 80));
        while (await r.ReadAsync()) {
            var v = Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString() ?? "").ToArray();
            Console.WriteLine(string.Join(" | ", v));
        }
    } catch (Exception ex) {
        Console.WriteLine($"ERR: {ex.Message}");
    }
}
