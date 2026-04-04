using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeTrack.Backend.Infrastructure.Persistence;

/// <summary>
/// Factory para design-time do EF Core (migrations)
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimeTrackDbContext>
{
    public TimeTrackDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimeTrackDbContext>();

        // Try to get connection string from environment or use default
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=timetrack;Username=postgres;Password=postgres";

        // Convert Neon URL if needed
        if (connectionString.StartsWith("postgres://"))
        {
            connectionString = ConvertNeonUrlToConnectionString(connectionString);
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new TimeTrackDbContext(optionsBuilder.Options, null);
    }

    private static string ConvertNeonUrlToConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = uri.Query.Contains("sslmode=require")
                ? Npgsql.SslMode.Require
                : uri.Query.Contains("sslmode=disable")
                    ? Npgsql.SslMode.Disable
                    : Npgsql.SslMode.Prefer
        };

        return builder.ToString();
    }
}
