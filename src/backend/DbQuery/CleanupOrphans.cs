using Npgsql;
using System;

// Run with: dotnet run --project DbQuery --cleanup
// Cleanup orphaned idempotency_keys

var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

Console.WriteLine("=== CLEANUP ORPHANED IDEMPOTENCY KEYS ===");
Console.WriteLine($"Timestamp: {DateTime.UtcNow:O}");

using var connection = new NpgsqlConnection(connectionString);
connection.Open();

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
