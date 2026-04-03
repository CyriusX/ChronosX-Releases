using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.Agent.Infrastructure.Persistence;
using TimeTrack.Agent.Infrastructure.Providers.Windows;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Infrastructure.Extensions;

/// <summary>
/// Extension methods para configurar serviços de infraestrutura
/// </summary>
[SupportedOSPlatform("windows")]
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
        services.AddSingleton<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<ISyncErrorRepository, SyncErrorRepository>();
        services.AddSingleton<ILocalSettingsRepository, LocalSettingsRepository>();
        services.AddSingleton<IFocusCycleRepository, FocusCycleRepository>();
        services.AddSingleton<IIdempotencyKeyGenerator, IdempotencyKeyGenerator>();
        services.AddSingleton<IAppCategoryCacheRepository, AppCategoryCacheRepository>();
        services.AddSingleton<IAgentEventLogRepository, AgentEventLogRepository>();
        services.AddSingleton<IAgentEventLogger, AgentEventLogger>();

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
        // FilePathExtractor - Singleton para extrair caminhos de arquivos
        services.AddSingleton<IFilePathExtractor, WindowsFilePathExtractor>();

        // ActiveWindowProvider - Singleton para manter o hook ativo
        services.AddSingleton<IActiveWindowProvider, WindowsActiveWindowProvider>();

        // IdleDetector - Singleton para eficiência
        services.AddSingleton<IIdleDetector, WindowsIdleDetector>();

        // MachineMetricsProvider - Singleton para coleta de CPU/Memória/Disco
        services.AddSingleton<IMachineMetricsProvider, WindowsMachineMetricsProvider>();

        // Configuração padrão ActiveWindow
        services.Configure<ActiveWindowProviderOptions>(options =>
        {
            options.PollingIntervalMs = 250;
            options.CacheValidityMs = 500;
            options.DebounceMs = 200;
        });

        // Configuração padrão IdleDetector
        services.Configure<IdleDetectorOptions>(options =>
        {
            options.DefaultThresholdSeconds = 180;
            options.MinThresholdSeconds = 60;
            options.MaxThresholdSeconds = 600;
            options.PollingIntervalMs = 2000;
        });

        return services;
    }

    /// <summary>
    /// Adiciona serviços de sincronização com o backend
    /// </summary>
    public static IServiceCollection AddSyncServices(
        this IServiceCollection services,
        SyncSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        // Registra SyncSettings para injeção
        services.AddSingleton(settings);

        // Registra TokenStore com DPAPI (singleton — must share in-memory cache across all consumers)
        services.AddHttpClient("TokenStore", client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });
        services.AddSingleton<ITokenStore>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("TokenStore");
            var logger = sp.GetRequiredService<ILogger<DpapiTokenStore>>();
            return new DpapiTokenStore(logger, httpClient, settings);
        });

        // Registra CurrentUserContext (extrai user_id do JWT)
        services.AddSingleton<ICurrentUserContext, JwtCurrentUserContext>();

        // Registra HttpClient para sync
        services.AddHttpClient<ISyncTransport, HttpSyncTransport>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(settings.AuthToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.AuthToken);
            }
        });

        // Registra DeviceActivationService
        services.AddHttpClient<IDeviceActivationService, DeviceActivationService>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        // Registra BackendReportsClient (fetch past days' data from cloud)
        services.AddHttpClient<IBackendReportsClient, BackendReportsClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        return services;
    }

    /// <summary>
    /// Adiciona NullSyncTransport para modo local/teste (sem backend)
    /// </summary>
    public static IServiceCollection AddNullSyncTransport(this IServiceCollection services)
    {
        services.AddSingleton<ISyncTransport, NullSyncTransport>();
        services.AddSingleton<ITokenStore, NullTokenStore>();
        services.AddSingleton<ICurrentUserContext, JwtCurrentUserContext>();
        services.AddSingleton<IDeviceActivationService, NullDeviceActivationService>();
        services.AddSingleton<IBackendReportsClient, NullBackendReportsClient>();
        return services;
    }
}
