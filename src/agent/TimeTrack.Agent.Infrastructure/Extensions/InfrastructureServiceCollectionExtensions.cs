using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Infrastructure.Persistence;
using TimeTrack.Agent.Infrastructure.Providers.Windows;

namespace TimeTrack.Agent.Infrastructure.Extensions;

/// <summary>
/// Extension methods para configurar serviços de infraestrutura
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona serviços de persistência SQLite
    /// </summary>
    public static IServiceCollection AddSqlitePersistence(
        this IServiceCollection services,
        string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required", nameof(databasePath));

        // Garante que o diretório existe
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Registra SqliteContext como singleton (único para toda a aplicação)
        services.AddSingleton<SqliteContext>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteContext>>();
            return new SqliteContext(databasePath, logger);
        });

        // Registra repositórios
        services.AddSingleton<ITrackingStateRepository, TrackingStateRepository>();
        services.AddSingleton<IActivitySessionRepository, ActivitySessionRepository>();
        services.AddSingleton<IIdlePeriodRepository, IdlePeriodRepository>();

        return services;
    }

    /// <summary>
    /// Inicializa o schema do banco de dados
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<SqliteContext>();
        await context.InitializeSchemaAsync();
    }

    /// <summary>
    /// Adiciona providers do Windows (ActiveWindow, IdleDetector)
    /// </summary>
    public static IServiceCollection AddWindowsProviders(this IServiceCollection services)
    {
        // ActiveWindowProvider - Singleton para manter o hook ativo
        services.AddSingleton<IActiveWindowProvider, WindowsActiveWindowProvider>();

        // Configuração padrão
        services.Configure<ActiveWindowProviderOptions>(options =>
        {
            options.PollingIntervalMs = 250;
            options.CacheValidityMs = 500;
            options.DebounceMs = 200;
        });

        return services;
    }
}
