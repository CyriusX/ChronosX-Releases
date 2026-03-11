using Microsoft.Data.Sqlite;
using System;

var dbPath = "C:/Projetos/TimeTracking/timetrack.db";
if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found at: " + dbPath);
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// Check table exists
using (var tableCheck = connection.CreateCommand())
{
    tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='sync_outbox'";
    var tableName = tableCheck.ExecuteScalar();
    if (tableName == null)
    {
        Console.WriteLine("sync_outbox table not found!");
        return;
    }
}

using var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT id, entity_type, entity_id, attempt_count, sent_at, created_at
    FROM sync_outbox
    ORDER BY created_at DESC
    LIMIT 10";

using var reader = cmd.ExecuteReader();
Console.WriteLine("=== Last 10 Outbox Items ===");
Console.WriteLine($"{"ID",-38} | {"Type",-25} | {"EntityId",-38} | {"Attempts"}");
Console.WriteLine(new string('-', 130));

int count = 0;
int validCount = 0;
int emptyCount = 0;

while (reader.Read())
{
    count++;
    var id = reader.GetString(0);
    var type = reader.GetString(1);
    var entityId = reader.GetString(2);
    var attempts = reader.GetInt32(3);
    var sentAt = reader.IsDBNull(4) ? "NULL" : reader.GetString(4);

    if (string.IsNullOrWhiteSpace(entityId))
    {
        emptyCount++;
        Console.WriteLine($"{id} | {type,-25} | !!! EMPTY EntityId !!! | {attempts}");
    }
    else if (Guid.TryParse(entityId, out _))
    {
        validCount++;
        Console.WriteLine($"{id} | {type,-25} | {entityId} | {attempts} | Sent: {sentAt}");
    }
    else
    {
        Console.WriteLine($"{id} | {type,-25} | INVALID: {entityId} | {attempts}");
    }
}

Console.WriteLine();
Console.WriteLine("=== Summary ===");
Console.WriteLine($"Total items checked: {count}");
Console.WriteLine($"Valid EntityIds: {validCount}");
Console.WriteLine($"Empty EntityIds: {emptyCount}");

if (emptyCount > 0)
{
    Console.WriteLine();
    Console.WriteLine("WARNING: Found empty EntityIds! Bug still present.");
}
else if (validCount > 0)
{
    Console.WriteLine();
    Console.WriteLine("SUCCESS: All EntityIds are valid GUIDs!");
}
else
{
    Console.WriteLine();
    Console.WriteLine("No outbox items found. Start tracking to create items.");
}
