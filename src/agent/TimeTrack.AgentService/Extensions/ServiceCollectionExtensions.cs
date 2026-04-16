using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Agent.Application.Extensions;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Events;
using TimeTrack.AgentService.Health;
using TimeTrack.AgentService.Workers;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.AgentService.Notifications;
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

        // Update settings
        services.Configure<UpdateSettings>(
            configuration.GetSection(UpdateSettings.SectionName));

        services.AddSingleton<UpdateSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateSettings>>().Value);

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

        var dbPath = settings.DatabasePath ?? "timetrack.db";

        // Resolve relative paths to %LOCALAPPDATA%\TimeTrack\<name> so the same DB file
        // is used regardless of the process working directory.
        if (!Path.IsPathRooted(dbPath))
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TimeTrack");
            Directory.CreateDirectory(appDataDir);
            dbPath = Path.Combine(appDataDir, dbPath);
        }

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
    /// Adiciona serviços de sincronização.
    /// Usa NullSyncTransport se não houver backend configurado.
    /// </summary>
    public static IServiceCollection AddSyncServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        // Check if we have a valid backend URL configured
        var backendUrl = settings.Sync?.BackendUrl;
        var useNullTransport = string.IsNullOrWhiteSpace(backendUrl) ||
                               !Uri.TryCreate(backendUrl, UriKind.Absolute, out _);

        Console.WriteLine($"[SyncServices] BackendUrl='{backendUrl}', useNullTransport={useNullTransport}");
        if (useNullTransport)
        {
            Console.WriteLine("[SyncServices] Using NULL sync transport (no backend)");
            services.AddNullSyncTransport();
        }
        else
        {
            Console.WriteLine("[SyncServices] Using REAL sync transport (HttpSyncTransport + BackendReportsClient)");
            services.AddSyncServices(settings.Sync!);
        }

        return services;
    }

    /// <summary>
    /// Adiciona Use Cases do Application layer
    /// </summary>
    public static IServiceCollection AddApplicationLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationUseCases();

        // Register AppCategorySyncService with HttpClient for category cache sync
        // Uses the same backend URL as other HTTP clients (from Sync config section)
        var settings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();
        var backendUrl = settings.Sync?.BackendUrl ?? "http://localhost:5000";

        services.AddHttpClient<IAppCategorySyncService, AppCategorySyncService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Heartbeat service
        services.AddHttpClient<IHeartbeatService, Agent.Infrastructure.Services.HeartbeatService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Remote command executor + service
        services.AddSingleton<Agent.Infrastructure.Services.IRemoteCommandExecutor, RemoteCommands.RemoteCommandExecutor>();
        services.AddSingleton<Func<Agent.Infrastructure.Services.IRemoteCommandExecutor>>(sp =>
            () => sp.GetRequiredService<Agent.Infrastructure.Services.IRemoteCommandExecutor>());

        services.AddHttpClient<IRemoteCommandService, Agent.Infrastructure.Services.RemoteCommandService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// Adiciona serviços de notificação e modo de foco
    /// </summary>
    public static IServiceCollection AddNotificationAndFocusMode(this IServiceCollection services)
    {
        // Notification service - forwards to DesktopHost via IPC for Windows toasts
        services.AddSingleton<IpcNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<IpcNotificationService>());

        // Focus mode services
        services.AddFocusModeServices();

        return services;
    }

    /// <summary>
    /// Adiciona workers do Agent
    /// </summary>
    public static IServiceCollection AddAgentWorkers(this IServiceCollection services)
    {
        services.AddSingleton<AgentStatusEventBroadcaster>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentStatusEventBroadcaster>());

        // Broadcast refreshed tokens to the UI so localStorage stays in sync
        services.AddSingleton<TokenRefreshBroadcaster>();

        services.AddHostedService<TrackingWorker>();
        services.AddHostedService<SyncWorker>();
        services.AddHostedService<MachineMetricsWorker>();
        services.AddHostedService<FocusModeEventBroadcaster>();
        services.AddSingleton<ActivityResumeState>();
        services.AddHostedService<ActivityResumeDetector>();
        services.AddHostedService<TaskIdleWatcher>();

        return services;
    }

    /// <summary>
    /// Adiciona serviços de atualização automática
    /// </summary>
    public static IServiceCollection AddUpdateServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var updateSettings = configuration.GetSection(UpdateSettings.SectionName).Get<UpdateSettings>()
            ?? new UpdateSettings();

        // Register Contracts.UpdateSettings required by UpdateService (Application layer)
        services.Configure<Agent.Contracts.Configuration.UpdateSettings>(
            configuration.GetSection(UpdateSettings.SectionName));
        services.AddSingleton<Agent.Contracts.Configuration.UpdateSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Agent.Contracts.Configuration.UpdateSettings>>().Value);

        // HTTP Client for updates
        services.AddHttpClient<IUpdateHttpClient, UpdateHttpClient>(client =>
        {
            var uri = new Uri(updateSettings.UpdateUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(updateSettings.DownloadTimeoutMinutes);
        });

        // Update service
        services.AddSingleton<IUpdateService, UpdateService>();

        // Event broadcaster
        services.AddSingleton<UpdateEventBroadcaster>();

        // Background worker (only if enabled)
        if (updateSettings.Enabled)
        {
            services.AddHostedService<UpdateWorker>();
        }

        return services;
    }

    /// <summary>
    /// Adiciona IPC Server para comunicação com DesktopHost
    /// </summary>
    public static IServiceCollection AddIpcServer(this IServiceCollection services)
    {
        // Register all IPC handlers
        services.AddIpcHandlers();

        // Register IPC server as both HostedService and IIpcServer
        services.AddSingleton<AgentService.Ipc.NamedPipeIpcServer>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentService.Ipc.NamedPipeIpcServer>());
        services.AddSingleton<AgentService.Ipc.IIpcServer>(sp => sp.GetRequiredService<AgentService.Ipc.NamedPipeIpcServer>());

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
