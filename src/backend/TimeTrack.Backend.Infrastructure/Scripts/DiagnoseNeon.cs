using Npgsql;
using System;

class DiagnosticRunner
{
    static void Main()
    {
        // Connection string from appsettings.Development.json
        var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

        Console.WriteLine("=== DIAGNOSTIC CHECK COMPLETED ===\n");
        Console.WriteLine($"Timestamp: {DateTime.UtcNow:O}\n");

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        Console.WriteLine("\n=== DATABASE VERIFICATION ===\n");

        // Check if we have data in activity_sessions
        var countQuery = "SELECT COUNT(*) FROM activity_sessions";
        using (var cmd = new NpgsqlCommand(countQuery, connection))
        {
            var currentCount = (long)cmd.ExecuteScalar();
            Console.WriteLine(currentCount > 0
                ? $"Data EXISTS in activity_sessions table ({currentCount} records)"
                : "NO data found in activity_sessions table");
        }

        // Check for data in idempotency_keys
        var idempotencyQuery = @"
            SELECT COUNT(*)
            FROM idempotency_keys
            WHERE entity_type = 'ActivitySession'
        ";
        using (var cmd = new NpgsqlCommand(idempotencyQuery, connection))
        {
            var idempotencyCount = (long)cmd.ExecuteScalar();
            Console.WriteLine($"Idempotency keys for ActivitySession: {idempotencyCount} records");
        }

        // Check recent activity_sessions
        var recentQuery = @"
            SELECT id, process_name, started_at, ended_at, idempotency_key
            FROM activity_sessions
            ORDER BY started_at DESC
            LIMIT 5
        ";
        using (var cmd = new NpgsqlCommand(recentQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            Console.WriteLine("\n--- Recent Activity Sessions ---");
            while (reader.Read())
            {
                Console.WriteLine($"ID: {reader["id"]}, Process: {reader["process_name"]}, Started: {reader["started_at"]}, IdempotencyKey: {reader["idempotency_key"]}");
            }
        }

        // Check for orphaned idempotency keys (exist in idempotency_keys but not in activity_sessions)
        var orphanQuery = @"
            SELECT key, entity_id
            FROM idempotency_keys
            WHERE entity_type = 'ActivitySession'
              AND entity_id NOT IN (SELECT id FROM activity_sessions)
        ";
        using (var cmd = new NpgsqlCommand(orphanQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            var hasOrphans = false;
            Console.WriteLine("\n--- Orphaned Idempotency Keys ---");
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

        // Check for orphaned activity sessions (exist in activity_sessions but not in idempotency_keys)
        var orphanActivityQuery = @"
            SELECT id, process_name, started_at
            FROM activity_sessions
            WHERE id NOT IN (SELECT entity_id FROM idempotency_keys WHERE entity_type = 'ActivitySession')
        ";
        using (var cmd = new NpgsqlCommand(orphanActivityQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            var hasOrphans = false;
            Console.WriteLine("\n--- Orphaned Activity Sessions ---");
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

        // Check devices table
        var devicesQuery = @"
            SELECT id, hostname, status, activated_at
            FROM devices
            ORDER BY activated_at DESC
            LIMIT 5
        ";
        using (var cmd = new NpgsqlCommand(devicesQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            Console.WriteLine("\n--- Devices ---");
            while (reader.Read())
            {
                Console.WriteLine($"ID: {reader["id"]}, Hostname: {reader["hostname"]}, Status: {reader["status"]}, Activated: {reader["activated_at"]}");
            }
        }

        Console.WriteLine("\n=== DIAGNOSTIC CHECK COMPLETED ===");
    }
}
