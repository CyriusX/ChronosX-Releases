using System;
using System.IO;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    'TimeTrack',
    'timetrack.db'
);

Console.WriteLine(Database: { dbPath});
Console.WriteLine(Exists: { File.Exists(dbPath)});

if (!File.Exists(dbPath))
{
    Console.WriteLine('Database not found!');
    return;
}

using var connection = new SqliteConnection(Data Source ={ dbPath });
connection.Open();

// Check tables
var tablesCmd = connection.CreateCommand();
tablesCmd.CommandText = 'SELECT name FROM sqlite_master WHERE type=''table''';
Console.WriteLine('\n=== TABLES ===');
using (var reader = tablesCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine(- { reader['name']});
    }
}

// Check outbox status
var outboxCmd = connection.CreateCommand();
outboxCmd.CommandText = @'
    SELECT
        COUNT(*) as total,
        SUM(CASE WHEN sent_at IS NULL THEN 1 ELSE 0 END) as pending,
        SUM(CASE WHEN sent_at IS NOT NULL THEN 1 ELSE 0 END) as sent,
        SUM(CASE WHEN last_error IS NOT NULL THEN 1 ELSE 0 END) as errors
    FROM sync_outbox';
Console.WriteLine('\n=== OUTBOX STATUS ===');
using (var reader = outboxCmd.ExecuteReader())
{
    if (reader.Read())
    {
        Console.WriteLine(Total: { reader['total']}, Pending: { reader['pending']}, Sent: { reader['sent']}, Errors: { reader['errors']});
    }
}

// Check recent errors
var errorsCmd = connection.CreateCommand();
errorsCmd.CommandText = @'
    SELECT entity_type, last_error, attempt_count
    FROM sync_outbox
    WHERE last_error IS NOT NULL
    ORDER BY created_at DESC
    LIMIT 5';
Console.WriteLine('\n=== RECENT ERRORS ===');
using (var reader = errorsCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine({ reader['entity_type']}: { reader['last_error']}
        (attempts: { reader['attempt_count']}));
    }
}

// Check activity sessions today
var todayCmd = connection.CreateCommand();
todayCmd.CommandText = @'
    SELECT app_display_name, SUM(duration_seconds) as total
    FROM activity_sessions
    WHERE DATE(started_at) = DATE(''now'')
    GROUP BY app_display_name
    ORDER BY total DESC
    LIMIT 10';
Console.WriteLine('\n=== TODAY''S ACTIVITY (Local SQLite) ===');
using (var reader = todayCmd.ExecuteReader())
{
    while (reader.Read())
    {
        var total = Convert.ToInt64(reader['total']);
        Console.WriteLine({ reader['app_display_name']}: { total / 3600}
        h { (total % 3600) / 60}
        m);
    }
}

// Check tracking state
var trackingStateCmd = connection.CreateCommand();
trackingStateCmd.CommandText = @'
    SELECT user_id, status, is_paused, is_active
    FROM tracking_state
    LIMIT 5';
Console.WriteLine('\n=== TRACKING STATE ===');
using (var reader = trackingStateCmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine(User: { reader['user_id']}, Status: { reader['status']}, Paused: { reader['is_paused']}, Active: { reader['is_active']});
    }
}

connection.Close();
