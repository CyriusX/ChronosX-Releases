// Quick verification script - run with: dotnet script verify_outbox.csx
#r "nuget: Microsoft.Data.Sqlite, 8.0.0"

using Microsoft.Data.Sqlite;
using System;

var dbPath = "C:/Projetos/TimeTracking/timetrack.db";
if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found!");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

using var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT id, entity_type, entity_id, attempt_count, sent_at, created_at
    FROM sync_outbox
    ORDER BY created_at DESC
    LIMIT 10";

using var reader = cmd.ExecuteReader();
Console.WriteLine("=== Last 10 Outbox Items ===");
Console.WriteLine($"{"ID",-36} | {"Type",-20} | {"EntityId",-36} | {"Attempts"}");
Console.WriteLine(new string('-', 120));

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
        Console.WriteLine($"{id} | {type,-20} | EMPTY EntityId! | {attempts}");
    }
    else if (Guid.TryParse(entityId, out _))
    {
        validCount++;
        Console.WriteLine($"{id} | {type,-20} | {entityId} | {attempts} | Sent: {sentAt}");
    }
    else
    {
        Console.WriteLine($"{id} | {type,-20} | INVALID: {entityId} | {attempts}");
    }
}

Console.WriteLine($"\n=== Summary ===");
Console.WriteLine($"Total: {count}, Valid: {validCount}, Empty: {emptyCount}");
