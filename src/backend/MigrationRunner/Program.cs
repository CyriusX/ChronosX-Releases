using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;

var connectionString = "Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable";

Console.WriteLine("Connecting to database...");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("Connected!");

// Verificar estado atual - tabela lowercase (como o EF Core espera)
Console.WriteLine("\n=== Current __ef_migrations_history (lowercase - EF Core expects this) ===");
try
{
    await using var checkCmd = new NpgsqlCommand(
        @"SELECT ""MigrationId"", ""ProductVersion"" FROM __ef_migrations_history ORDER BY ""MigrationId""",
        connection);
    await using var reader = await checkCmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"  {reader.GetString(0)} | {reader.GetString(1)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Table does not exist or error: {ex.Message}");
}

// Verificar tabela maiúscula (incorreta - deve ser removida)
Console.WriteLine("\n=== Checking for incorrect uppercase table ===");
try
{
    await using var checkCmd = new NpgsqlCommand(
        @"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory' AND table_schema = 'public'",
        connection);
    var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

    if (count > 0)
    {
        Console.WriteLine("  Found incorrect table __EFMigrationsHistory (uppercase)");
        Console.WriteLine("  Dropping incorrect table...");

        await using var dropCmd = new NpgsqlCommand(@"DROP TABLE IF EXISTS ""__EFMigrationsHistory""", connection);
        await dropCmd.ExecuteNonQueryAsync();
        Console.WriteLine("  ✓ Dropped __EFMigrationsHistory (uppercase)");
    }
    else
    {
        Console.WriteLine("  No incorrect uppercase table found.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

// Verificar tabelas existentes
Console.WriteLine("\n=== Tables in database ===");
await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name", connection))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    var tables = new List<string>();
    while (await reader.ReadAsync())
    {
        tables.Add(reader.GetString(0));
        Console.WriteLine($"  {reader.GetString(0)}");
    }
}

// Se argumento "check" foi passado, apenas verificar
if (args.Length > 0 && args[0] == "check")
{
    Console.WriteLine("\n✅ Check complete!");
    return;
}

// Aceita arquivo SQL como argumento ou usa sync_migration_history.sql como padrão
var fileName = args.Length > 0 ? args[0] : "sync_migration_history.sql";
var sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
if (!File.Exists(sqlPath))
{
    sqlPath = fileName;
}

if (!File.Exists(sqlPath))
{
    Console.WriteLine($"\nMigration file not found at: {sqlPath}");
    Console.WriteLine("Usage: dotnet run -- [check|<sql-file>]");
    return;
}

var sql = await File.ReadAllTextAsync(sqlPath);
Console.WriteLine($"\nRunning: {fileName}");

await using var command = new NpgsqlCommand(sql, connection);
await command.ExecuteNonQueryAsync();

Console.WriteLine("Migration executed successfully!");

// Verificar resultado
Console.WriteLine("\n=== Updated __ef_migrations_history ===");
await using var verifyCmd = new NpgsqlCommand(
    @"SELECT ""MigrationId"", ""ProductVersion"" FROM __ef_migrations_history ORDER BY ""MigrationId""",
    connection);
await using var verifyReader = await verifyCmd.ExecuteReaderAsync();

while (await verifyReader.ReadAsync())
{
    Console.WriteLine($"  ✓ {verifyReader.GetString(0)} (v{verifyReader.GetString(1)})");
}

Console.WriteLine("\n✅ Done!");
