// Run this script to execute the migration
// dotnet script run_migration.csx

#load "add_daily_focus_scores_table.sql"

using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;

var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

var sql = File.ReadAllText("add_daily_focus_scores_table.sql");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

await using var command = new NpgsqlCommand(sql, connection);
await command.ExecuteNonQueryAsync();

Console.WriteLine("Migration executed successfully!");
