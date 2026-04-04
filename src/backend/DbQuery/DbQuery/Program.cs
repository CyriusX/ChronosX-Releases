using Npgsql;
using System;

// Run with: dotnet run --project DbQuery [cleanup|reset|nuke]
// Database diagnostic script
// Pass "cleanup" to delete orphaned idempotency_keys
// Pass "reset" to delete all data before today
// Pass "nuke" to delete ALL data (for clean slate)

var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";
var shouldCleanup = args.Length > 0 && args[0].Equals("cleanup", StringComparison.OrdinalIgnoreCase);
var shouldReset = args.Length > 0 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase);
var shouldNuke = args.Length > 0 && args[0].Equals("nuke", StringComparison.OrdinalIgnoreCase);
var cutoffDate = DateTime.UtcNow.Date;

// Skip diagnostics if nuke mode
if (shouldNuke)
{
    Console.WriteLine("\n=== NUKE ALL DATA ===");
    return;
}

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

// Check orphaned idempotency keys (exist in idempotency_keys but not in activity_sessions)
Console.WriteLine("\n--- Orphaned idempotency_keys ---");
using (var cmd = new NpgsqlCommand(@"
    SELECT COUNT(*)
    FROM idempotency_keys
    WHERE entity_type = 'ActivitySession'
      AND entity_id NOT IN (SELECT id FROM activity_sessions)", connection))
{
    var orphanCount = (long)cmd.ExecuteScalar();
    Console.WriteLine($"Orphaned count: {orphanCount}");
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

Console.WriteLine("=== DIAGNOSTIC COMPLETED ===");

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

// Reset old data if requested
if (shouldReset)
{
    Console.WriteLine($"\n=== RESET OLD DATA (before {cutoffDate:yyyy-MM-dd}) ===");

    using var transaction = connection.BeginTransaction();

    try
    {
        // Delete old activity sessions
        using (var cmd = new NpgsqlCommand("DELETE FROM activity_sessions WHERE date(started_at) < @cutoff", connection, transaction))
        {
            cmd.Parameters.AddWithValue("cutoff", cutoffDate);
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted activity_sessions: {deleted}");
        }

        // Delete orphaned idempotency keys
        using (var cmd = new NpgsqlCommand(@"
            DELETE FROM idempotency_keys
            WHERE entity_type = 'ActivitySession'
              AND entity_id NOT IN (SELECT id FROM activity_sessions)", connection, transaction))
        {
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted orphaned idempotency_keys: {deleted}");
        }

        // Delete old idle periods
        using (var cmd = new NpgsqlCommand("DELETE FROM idle_periods WHERE date(started_at) < @cutoff", connection, transaction))
        {
            cmd.Parameters.AddWithValue("cutoff", cutoffDate);
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted idle_periods: {deleted}");
        }

        transaction.Commit();
        Console.WriteLine("\n=== RESET COMPLETED ===");
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        Console.WriteLine($"ERROR: {ex.Message}");
    }

    // Show remaining
    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM activity_sessions", connection))
    {
        Console.WriteLine($"\nRemaining activity_sessions: {cmd.ExecuteScalar()}");
    }
}

// Nuke all data if requested
if (shouldNuke)
{
    Console.WriteLine("\n=== NUKE ALL DATA ===");

    using var transaction = connection.BeginTransaction();

    try
    {
        // Delete ALL activity sessions
        using (var cmd = new NpgsqlCommand("DELETE FROM activity_sessions", connection, transaction))
        {
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted activity_sessions: {deleted}");
        }

        // Delete ALL idempotency keys for ActivitySession
        using (var cmd = new NpgsqlCommand("DELETE FROM idempotency_keys WHERE entity_type = 'ActivitySession'", connection, transaction))
        {
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted idempotency_keys (ActivitySession): {deleted}");
        }

        // Delete ALL idle periods
        using (var cmd = new NpgsqlCommand("DELETE FROM idle_periods", connection, transaction))
        {
            var deleted = cmd.ExecuteNonQuery();
            Console.WriteLine($"Deleted idle_periods: {deleted}");
        }

        transaction.Commit();
        Console.WriteLine("\n=== NUKE COMPLETED ===");
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        Console.WriteLine($"ERROR: {ex.Message}");
    }

    // Show remaining
    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM activity_sessions", connection))
    {
        Console.WriteLine($"\nRemaining activity_sessions: {cmd.ExecuteScalar()}");
    }
    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM idempotency_keys WHERE entity_type = 'ActivitySession'", connection))
    {
        Console.WriteLine($"Remaining idempotency_keys: {cmd.ExecuteScalar()}");
    }
}
