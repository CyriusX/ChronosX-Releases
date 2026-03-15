// Run this script to execute the migration
// dotnet script run_migration.csx

#load "add_daily_focus_scores_table.sql"

using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;

var connectionString = "Host=ep-billowing-snow-adnjkioj-pooler.c-2.us-east-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_y6PlhoBZJ9QS;sslmode=require";

var sql = File.ReadAllText("add_daily_focus_scores_table.sql");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

await using var command = new NpgsqlCommand(sql, connection);
await command.ExecuteNonQueryAsync();

Console.WriteLine("Migration executed successfully!");
