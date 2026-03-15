using Npgsql;
using System;
using System.Threading.Tasks;

var connectionString = "Host=ep-billowing-snow-adnjkioj-pooler.c-2.us-east-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_y6PlhoBZJ9QS;sslmode=require";

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

Console.WriteLine("=== __EFMigrationsHistory ===");
await using (var cmd = new NpgsqlCommand(@"SELECT ""MigrationId"", ""ProductVersion"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId""", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"  {reader.GetString(0)} | {reader.GetString(1)}");
    }
}

Console.WriteLine("\n=== Tables in database ===");
await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"  {reader.GetString(0)}");
    }
}
