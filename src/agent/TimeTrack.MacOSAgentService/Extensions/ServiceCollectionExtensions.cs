using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Agent.Application.Extensions;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.Agent.Infrastructure.MacOS.Extensions;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.MacOSAgentService.Configuration;
using TimeTrack.MacOSAgentService.Ipc;

namespace TimeTrack.MacOSAgentService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MacOSAgentSettings>(
            configuration.GetSection(MacOSAgentSettings.SectionName));

        services.AddSingleton<MacOSAgentSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MacOSAgentSettings>>().Value);

        // Register AgentSettings (Windows shared workers need this type)
        services.Configure<AgentSettings>(
            configuration.GetSection(AgentSettings.SectionName));

        services.AddSingleton<AgentSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentSettings>>().Value);

        services.Configure<UpdateSettings>(
            configuration.GetSection(UpdateSettings.SectionName));

        services.AddSingleton<UpdateSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateSettings>>().Value);

        return services;
    }

    public static IServiceCollection AddInfrastructurePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(MacOSAgentSettings.SectionName).Get<MacOSAgentSettings>()
            ?? new MacOSAgentSettings();

        var dbPath = settings.DatabasePath ?? "timetrack.db";

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

    public static IServiceCollection AddMacOSInfrastructureProviders(this IServiceCollection services)
    {
        services.AddMacOSProviders();
        return services;
    }

    public static IServiceCollection AddSyncServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(MacOSAgentSettings.SectionName).Get<MacOSAgentSettings>()
            ?? new MacOSAgentSettings();

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
            Console.WriteLine("[SyncServices] Using REAL sync transport");
            services.AddSyncServices(new Agent.Contracts.Configuration.SyncSettings
            {
                BackendUrl = settings.Sync!.BackendUrl!,
                AuthToken = settings.Sync.AuthToken,
                SyncIntervalSeconds = settings.Sync.SyncIntervalSeconds,
                MaxBatchSize = settings.Sync.MaxBatchSize,
                MaxBatchSizeBytes = (int)settings.Sync.MaxBatchSizeBytes,
                HttpTimeoutSeconds = settings.Sync.HttpTimeoutSeconds,
                ConsecutiveFailuresAlertThreshold = settings.Sync.ConsecutiveFailuresAlertThreshold
            });
        }

        return services;
    }

    public static IServiceCollection AddApplicationLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationUseCases();

        var settings = configuration.GetSection(MacOSAgentSettings.SectionName).Get<MacOSAgentSettings>()
            ?? new MacOSAgentSettings();
        var backendUrl = string.IsNullOrWhiteSpace(settings.Sync?.BackendUrl)
            ? "https://chronosx-timetrack-api.gpoda0.easypanel.host"
            : settings.Sync!.BackendUrl!;

        services.AddHttpClient<IAppCategorySyncService, AppCategorySyncService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IHeartbeatService, HeartbeatService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<IRemoteCommandExecutor, TimeTrack.MacOSAgentService.RemoteCommands.MacOSRemoteCommandExecutor>();
        services.AddSingleton<Func<IRemoteCommandExecutor>>(sp =>
            () => sp.GetRequiredService<IRemoteCommandExecutor>());

        services.AddHttpClient<IRemoteCommandService, Agent.Infrastructure.Services.RemoteCommandService>(client =>
        {
            client.BaseAddress = new Uri(backendUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    public static IServiceCollection AddNotificationAndFocusMode(this IServiceCollection services)
    {
        services.AddSingleton<AgentService.Notifications.IpcNotificationService>();
        services.AddSingleton<INotificationService>(sp =>
            sp.GetRequiredService<AgentService.Notifications.IpcNotificationService>());

        services.AddFocusModeServices();

        return services;
    }

    public static IServiceCollection AddIpcServer(this IServiceCollection services)
    {
        services.AddIpcHandlers();

        services.AddSingleton<UnixDomainSocketIpcServer>();
        services.AddHostedService(sp => sp.GetRequiredService<UnixDomainSocketIpcServer>());
        services.AddSingleton<IIpcServer>(sp => sp.GetRequiredService<UnixDomainSocketIpcServer>());

        return services;
    }

    public static IServiceCollection AddUpdateServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var updateSettings = configuration.GetSection(UpdateSettings.SectionName).Get<UpdateSettings>()
            ?? new UpdateSettings();

        services.Configure<Agent.Contracts.Configuration.UpdateSettings>(
            configuration.GetSection(UpdateSettings.SectionName));
        services.AddSingleton<Agent.Contracts.Configuration.UpdateSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Agent.Contracts.Configuration.UpdateSettings>>().Value);

        services.AddHttpClient<IUpdateHttpClient, UpdateHttpClient>(client =>
        {
            var uri = new Uri(updateSettings.UpdateUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(updateSettings.DownloadTimeoutMinutes);
        });

        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<AgentService.Events.UpdateEventBroadcaster>();

        if (updateSettings.Enabled)
        {
            services.AddHostedService<AgentService.Workers.UpdateWorker>();
        }

        return services;
    }
}
