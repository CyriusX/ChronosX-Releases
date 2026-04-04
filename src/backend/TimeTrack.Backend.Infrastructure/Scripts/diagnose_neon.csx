#!/usr/bin/env dotnet-script
#r "nuget: Npgsql, 8.0.0"
#r "nuget: Dapper, 2.1.21"

using Npgsql;
using Dapper;
using System;
using System.IO;
using System.Threading.Tasks;

class DiagnosticRunner
{
    static async Task Main()
    {
        // Connection string from appsettings.Development.json
        var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

        Console.WriteLine("=== DIAGNOSTIC CHECK COMPLETED ===\n");
        Console.WriteLine($"Timestamp: {DateTime.UtcNow:O}\n");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Console.WriteLine("\n=== DATABASE VERIFICATION ===\n");

        // Run verification queries
        Console.WriteLine("\n--- VERIFYING activity_sessions TABLE ---");
        var sessionQuery = @"
            SELECT COUNT(*)
            FROM activity_sessions
            WHERE started_at > now() - INTERVAL '7 days'
            ORDER BY started_at DESC
            LIMIT 5
        ";

        var sessionsData = (await connection.QueryAsync<dynamic>(sessionQuery)).ToList();
        foreach (var session in sessionsData)
        {
            Console.WriteLine($"Activity session: {session.id}, Process: {session.process_name}, Started: {session.started_at}");
        }

        // Check if we have data in activity_sessions
        var countQuery = "SELECT COUNT(*) FROM activity_sessions";
        var currentCount = (await connection.QueryAsync<int>(countQuery));

        Console.WriteLine(currentCount > 0
            ? $"Data EXISTS in activity_sessions table ({currentCount} records)"
            : $"NO data found in activity_sessions table");

        // Check for data in idempotency_keys
        var idempotencyQuery = @"
            SELECT COUNT(*)
            FROM idempotency_keys
            WHERE entity_type = 'ActivitySession'
        ";
        var idempotencyCount = (await connection.QueryAsync<int>(idempotencyQuery));
        Console.WriteLine($"Idempotency keys for ActivitySession: {idempotencyCount} records");

        // Check for orphaned idempotency keys (exist in idempotency_keys but not in activity_sessions)
        var orphanQuery = @"
            SELECT key, entity_id
            FROM idempotency_keys
            WHERE entity_type = 'ActivitySession'
              AND entity_id NOT IN (SELECT id FROM activity_sessions)
        ";
        var orphanedKeys = (await connection.QueryAsync<dynamic>(orphanQuery)).ToList();
        if (orphanedKeys.Any())
        {
            Console.WriteLine($"WARNING: Found {orphanedKeys.Count} orphaned idempotency keys (keys not in activity_sessions)");
            foreach (var orphan in orphanedKeys)
            {
                Console.WriteLine($"  - Key: {orphan.key}, EntityId: {orphan.entity_id}");
            }
        }
        else
        {
            Console.WriteLine("All idempotency keys have corresponding activity sessions");
        }

        // Check for orphaned activity sessions (exist in activity_sessions but not in idempotency_keys)
        var orphanActivityQuery = @"
            SELECT id, process_name, started_at
            FROM activity_sessions
            WHERE id NOT IN (SELECT entity_id FROM idempotency_keys WHERE entity_type = 'ActivitySession')
        ";
        var orphanedActivities = (await connection.QueryAsync<dynamic>(orphanActivityQuery)).ToList();
        if (orphanedActivities.Any())
        {
            Console.WriteLine($"WARNING: Found {orphanedActivities.Count} orphaned activity sessions (not in idempotency_keys)");
            foreach (var orphan in orphanedActivities)
            {
                Console.WriteLine($"  - ID: {orphan.id}, Process: {orphan.process_name}, Started: {orphan.started_at}");
            }
        }
        else
        {
            Console.WriteLine("All activity sessions have corresponding idempotency keys");
        }

        // Check for data consistency
        var checkConsistencyQuery = @"
            SELECT
                a.id as activity_id,
                a.process_name,
                a.started_at,
                a.ended_at,
                i.key as idempotency_key,
                i.entity_id
            FROM activity_sessions a
            LEFT JOIN idempotency_keys i
                ON a.id = i.entity_id
                AND i.entity_type = 'ActivitySession'
            ORDER BY a.started_at DESC
            LIMIT 5
        ";

        var consistentData = (await connection.QueryAsync<dynamic>(checkConsistencyQuery)).ToList();
        foreach (var row in consistentData)
        {
            Console.WriteLine($"CONSISTENCY CHECK: ActivitySession {row.activity_id}, Process: {row.process_name}");
                Console.WriteLine($"  - IdempotencyKey: {row.idempotency_key}");
                Console.WriteLine($"  - ActivitySession exists: {row.activity_id_exists}");
                Console.WriteLine($"  - IdempotencyKey.EntityId: {row.entity_id}");
            if (!row.activity_id_exists && row.entity_id != null)
            {
                Console.WriteLine($"WARNING: Inconsistency detected!");
                Console.WriteLine($"  - ActivitySession record exists but referenced IdempotencyKey points to missing ActivitySession");
            }
        }
        else
        {
            Console.WriteLine("All data is consistent");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FATAL ERROR: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}
