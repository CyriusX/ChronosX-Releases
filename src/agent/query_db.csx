#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Data.Sqlite, 8.0.0"
#r "nuget: Dapper, 2.1.21"

using Microsoft.Data.Sqlite;
using Dapper;
using System;

var dbPath = args.Length > 0 ? args[0] : "TimeTrack.AgentService/timetrack.db";
var connectionString = $"Data Source={dbPath}";

using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("=== IDLE PERIODS ===");
var idleCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM idle_periods");
Console.WriteLine($"Total: {idleCount}");

var idlePeriods = connection.Query("SELECT id, user_id, start_utc, end_utc, threshold_seconds FROM idle_periods ORDER BY start_utc DESC LIMIT 5");
foreach (var p in idlePeriods)
{
    Console.WriteLine($"  - {p.id}: user={p.user_id}, {p.start_utc} -> {p.end_utc} ({p.threshold_seconds}s)");
}

Console.WriteLine("\n=== SYNC OUTBOX (Pending) ===");
var pendingCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NULL");
Console.WriteLine($"Pending items: {pendingCount}");

var pending = connection.Query("SELECT id, entity_type, entity_id, next_attempt_utc, last_error FROM sync_outbox WHERE sent_at IS NULL ORDER BY next_attempt_utc LIMIT 10");
foreach (var p in pending)
{
    Console.WriteLine($"  - [{p.entity_type}] {p.entity_id}: next={p.next_attempt_utc}, error={p.last_error}");
}

Console.WriteLine("\n=== SYNC OUTBOX (Sent) ===");
var sentCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM sync_outbox WHERE sent_at IS NOT NULL");
Console.WriteLine($"Sent items: {sentCount}");

var sent = connection.Query("SELECT id, entity_type, sent_at FROM sync_outbox WHERE sent_at IS NOT NULL ORDER BY sent_at DESC LIMIT 5");
foreach (var s in sent)
{
    Console.WriteLine($"  - [{s.entity_type}] sent at {s.sent_at}");
}

Console.WriteLine("\n=== SYNC ERRORS ===");
var errorCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM sync_errors");
Console.WriteLine($"Total errors: {errorCount}");

var errors = connection.Query("SELECT endpoint, status_code, error_message, timestamp_utc FROM sync_errors ORDER BY timestamp_utc DESC LIMIT 5");
foreach (var e in errors)
{
    Console.WriteLine($"  - [{e.status_code}] {e.endpoint}: {e.error_message} at {e.timestamp_utc}");
}

Console.WriteLine("\n=== ACTIVITY SESSIONS ===");
var sessionCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM activity_sessions");
Console.WriteLine($"Total: {sessionCount}");
