using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.Infrastructure.Integrations.Stripe;
using TimeTrack.Backend.Infrastructure.Persistence.Seeds;

namespace TimeTrack.Backend.Infrastructure.Persistence;

/// <summary>
/// Background service that initializes the database on startup
///
/// SRP: Only handles database initialization and seeding
/// OCP: Extensible for adding new seeders
///
/// Runs once on application startup to ensure:
/// - Database exists and migrations are applied
/// - Seed data is populated
/// </summary>
public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database initialization...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TimeTrackDbContext>();

        try
        {
            // Ensure database is created / migrations applied
            _logger.LogInformation("Applying database migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations applied successfully");

            // Run seed data
            _logger.LogInformation("Running seed data...");
            await AppCategoryGlobalSeed.SeedAsync(dbContext, cancellationToken);
            var plansOptions = scope.ServiceProvider.GetRequiredService<IOptions<PlansOptions>>();
            await SubscriptionPlanSeed.SeedAsync(dbContext, plansOptions, cancellationToken);
            _logger.LogInformation("Seed data completed");

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for database initialization
/// </summary>
public static class DatabaseInitializerExtensions
{
    /// <summary>
    /// Adds database initialization as a hosted service
    /// </summary>
    public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
