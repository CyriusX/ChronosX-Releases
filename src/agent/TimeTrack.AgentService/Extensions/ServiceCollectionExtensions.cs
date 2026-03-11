using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Agent.Application.Extensions;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Health;
using TimeTrack.AgentService.Workers;

namespace TimeTrack.AgentService.Extensions;

/// <summary>
/// Extension methods para configurar DI
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona configurações do Agent
    /// </summary>
    public static IServiceCollection AddAgentConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentSettings>(
            configuration.GetSection(AgentSettings.SectionName));

        services.AddSingleton<AgentSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentSettings>>().Value);

        return services;
    }

    /// <summary>
    /// Adiciona infraestrutura de persistência (SQLite)
    /// </summary>
    public static IServiceCollection AddInfrastructurePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        var dbPath = settings.DatabasePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TimeTrack", "timetrack.db");

        services.AddSqlitePersistence(dbPath);

        return services;
    }

    /// <summary>
    /// Adiciona providers de infraestrutura do Windows
    /// </summary>
    public static IServiceCollection AddInfrastructureProviders(this IServiceCollection services)
    {
        // Registra providers do Windows (ActiveWindow + IdleDetector)
        services.AddWindowsProviders();

        return services;
    }

    /// <summary>
    /// Adiciona serviços de sincronização
    /// </summary>
    public static IServiceCollection AddSyncServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        services.AddSyncServices(settings.Sync);

        return services;
    }

    /// <summary>
    /// Adiciona Use Cases do Application layer
    /// </summary>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddApplicationUseCases();
        return services;
    }

    /// <summary>
    /// Adiciona workers do Agent
    /// </summary>
    public static IServiceCollection AddAgentWorkers(this IServiceCollection services)
    {
        services.AddHostedService<TrackingWorker>();
        services.AddHostedService<SyncWorker>();

        return services;
    }

    /// <summary>
    /// Adiciona health checks do Agent
    /// </summary>
    public static IServiceCollection AddAgentHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        services.AddHealthChecks()
            .AddCheck<AgentHealthCheck>("agent_health", tags: new[] { "ready" });

        return services;
    }

    /// <summary>
    /// Configura prioridade do processo
    /// </summary>
    public static void ConfigureProcessPriority(string priority)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            process.PriorityClass = priority.ToUpperInvariant() switch
            {
                "BELOWNORMAL" => System.Diagnostics.ProcessPriorityClass.BelowNormal,
                "IDLE" => System.Diagnostics.ProcessPriorityClass.Idle,
                "NORMAL" => System.Diagnostics.ProcessPriorityClass.Normal,
                _ => System.Diagnostics.ProcessPriorityClass.BelowNormal
            };
        }
        catch
        {
            // Ignora erros de permissão
        }
    }
}
