using Npgsql;
using System;

// Run with: dotnet run --project DbQuery [cleanup]
// Database diagnostic script
// Pass "cleanup" argument to delete orphaned idempotency_keys

var connectionString = "Host=ep-billowing-snow-adnjkioj-pooler.c-2.us-east-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_y6PlhoBZJ9QS;sslmode=require";
var shouldCleanup = args.Length > 0 && args[0].Equals("cleanup", StringComparison.OrdinalIgnoreCase);

Console.WriteLine("=== DIAGNOSTIC CHECK ===");
Console.WriteLine($"Timestamp: {DateTime.UtcNow:O}");

using var connection = new NpgsqlConnection(connectionString);
connection.Open();

// Check activity_sessions count
using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM activity_sessions", connection))
{
    var count = (long)cmd.ExecuteScalar();
    Console.WriteLine($"activity_sessions count: {count}");
}

// Check idempotency_keys for ActivitySession
using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM idempotency_keys WHERE entity_type = 'ActivitySession'", connection))
{
    var count = (long)cmd.ExecuteScalar();
    Console.WriteLine($"idempotency_keys (ActivitySession): {count}");
}

// Check devices
using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM devices", connection))
{
    var count = (long)cmd.ExecuteScalar();
    Console.WriteLine($"devices count: {count}");
}

// Check recent activity_sessions
Console.WriteLine("\n--- Recent activity_sessions ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT id, process_name, started_at, ended_at, duration_seconds, idempotency_key
    FROM activity_sessions
    ORDER BY started_at DESC
    LIMIT 5", connection))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"ID: {reader["id"]}");
        Console.WriteLine($"  Process: {reader["process_name"]}");
        Console.WriteLine($"  Started: {reader["started_at"]}");
        Console.WriteLine($"  Ended: {reader["ended_at"]}");
        Console.WriteLine($"  Duration: {reader["duration_seconds"]}s");
        Console.WriteLine($"  IdempotencyKey: {reader["idempotency_key"]}");
        Console.WriteLine();
    }
}

// Check devices
Console.WriteLine("\n--- Devices ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT id, hostname, status, activated_at, last_heartbeat_at
    FROM devices
    ORDER BY activated_at DESC
    LIMIT 5", connection))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"ID: {reader["id"]}");
        Console.WriteLine($"  Hostname: {reader["hostname"]}");
        Console.WriteLine($"  Status: {reader["status"]}");
        Console.WriteLine($"  Activated: {reader["activated_at"]}");
        Console.WriteLine($"  Last heartbeat: {reader["last_heartbeat_at"]}");
        Console.WriteLine();
    }
}

// Check orphaned idempotency keys (exist in idempotency_keys but not in activity_sessions)
Console.WriteLine("\n--- Orphaned idempotency_keys ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT key, entity_id
    FROM idempotency_keys
    WHERE entity_type = 'ActivitySession'
      AND entity_id NOT IN (SELECT id FROM activity_sessions)
    LIMIT 10", connection))
using (var reader = cmd.ExecuteReader())
{
    var hasOrphans = false;
    while (reader.Read())
    {
        hasOrphans = true;
        Console.WriteLine($"WARNING: Orphan key: {reader["key"]}, EntityId: {reader["entity_id"]}");
    }
    if (!hasOrphans)
    {
        Console.WriteLine("No orphaned idempotency keys found");
    }
}

// Check orphaned activity sessions (exist in activity_sessions but not in idempotency_keys)
Console.WriteLine("\n--- Orphaned activity_sessions ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT id, process_name, started_at
    FROM activity_sessions
    WHERE id NOT IN (SELECT entity_id FROM idempotency_keys WHERE entity_type = 'ActivitySession')
    LIMIT 10", connection))
using (var reader = cmd.ExecuteReader())
{
    var hasOrphans = false;
    while (reader.Read())
    {
        hasOrphans = true;
        Console.WriteLine($"WARNING: Orphan session: {reader["id"]}, Process: {reader["process_name"]}, Started: {reader["started_at"]}");
    }
    if (!hasOrphans)
    {
        Console.WriteLine("No orphaned activity sessions found");
    }
}

// Check for data consistency
Console.WriteLine("\n--- Data Consistency Check ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT
        a.id,
        a.process_name,
        a.started_at,
        a.idempotency_key,
        i.key as ik_key,
        i.entity_id
    FROM activity_sessions a
    LEFT JOIN idempotency_keys i ON a.id = i.entity_id AND i.entity_type = 'ActivitySession'
    ORDER BY a.started_at DESC
    LIMIT 5", connection))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        Console.WriteLine($"ActivitySession: {reader["id"]}");
        Console.WriteLine($"  Process: {reader["process_name"]}");
        Console.WriteLine($"  Started: {reader["started_at"]}");
        Console.WriteLine($"  Session IdempotencyKey: {reader["idempotency_key"]}");
        Console.WriteLine($"  IdempotencyKey.Key: {reader["ik_key"]}");
        Console.WriteLine($"  IdempotencyKey.EntityId: {reader["entity_id"]}");
        Console.WriteLine();
    }
}

Console.WriteLine("\n=== DIAGNOSTIC COMPLETED ===");

// Cleanup orphaned records if requested
if (shouldCleanup)
{
    Console.WriteLine("\n=== CLEANUP ORPHANED IDEMPOTENCY KEYS ===");

    // Count orphaned keys before cleanup
    long orphanCount;
    using (var cmd = new NpgsqlCommand(@"
        SELECT COUNT(*)
        FROM idempotency_keys
        WHERE entity_type = 'ActivitySession'
          AND entity_id NOT IN (SELECT id FROM activity_sessions)", connection))
    {
        orphanCount = (long)cmd.ExecuteScalar();
        Console.WriteLine($"Orphaned idempotency_keys (ActivitySession): {orphanCount}");
    }

    if (orphanCount > 0)
    {
        Console.WriteLine($"\nDeleting {orphanCount} orphaned records...");

        using var transaction = connection.BeginTransaction();

        try
        {
            using (var cmd = new NpgsqlCommand(@"
                DELETE FROM idempotency_keys
                WHERE entity_type = 'ActivitySession'
                  AND entity_id NOT IN (SELECT id FROM activity_sessions)", connection, transaction))
            {
                var deleted = cmd.ExecuteNonQuery();
                Console.WriteLine($"Deleted: {deleted} records");
            }

            transaction.Commit();
            Console.WriteLine("Transaction committed successfully.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine("Transaction rolled back.");
        }
    }

    // Verify cleanup
    using (var cmd = new NpgsqlCommand(@"
        SELECT COUNT(*)
        FROM idempotency_keys
        WHERE entity_type = 'ActivitySession'
          AND entity_id NOT IN (SELECT id FROM activity_sessions)", connection))
    {
        var remaining = (long)cmd.ExecuteScalar();
        Console.WriteLine($"\nRemaining orphaned keys: {remaining}");
    }

    Console.WriteLine("\n=== CLEANUP COMPLETED ===");
}
