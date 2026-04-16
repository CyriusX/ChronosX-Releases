using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.AgentService.Extensions;
using TimeTrack.AgentService.Workers;

// Configura prioridade do processo o mais cedo possível
var priority = Environment.GetEnvironmentVariable("TIMETRACK_PROCESS_PRIORITY") ?? "BelowNormal";
ServiceCollectionExtensions.ConfigureProcessPriority(priority);

IHost host = Host.CreateDefaultBuilder(args)
    // Always use the exe's own directory as content root so appsettings.json is found
    // regardless of working directory (Task Scheduler, Windows Service, or console dev run).
    .UseContentRoot(AppContext.BaseDirectory)
    .UseWindowsService(options =>
    {
        options.ServiceName = "ChronosX Agent";
    })
    .ConfigureServices((context, services) =>
    {
        // By default, an unhandled exception in any BackgroundService stops the whole host.
        // We prefer resiliency: log and keep the agent service alive, relying on health/events
        // to surface issues instead of hard-crashing the tracking process.
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

        // Configuration
        services.AddAgentConfiguration(context.Configuration);

        // Infrastructure - SQLite Persistence
        services.AddInfrastructurePersistence(context.Configuration);

        // Infrastructure Providers (placeholders)
        services.AddInfrastructureProviders();

        // Sync Services (uses NullSyncTransport for local testing)
        services.AddSyncServices(context.Configuration);

        // Application Layer - Use Cases + Category Sync
        services.AddApplicationLayer(context.Configuration);

        // Notification and Focus Mode services
        services.AddNotificationAndFocusMode();

        // IPC Server for DesktopHost communication
        services.AddIpcServer();

        // Workers
        services.AddAgentWorkers();

        // Update Services
        services.AddUpdateServices(context.Configuration);
    })
    .Build();

// Inicializa o banco de dados na inicialização
using (var scope = host.Services.CreateScope())
{
    await scope.ServiceProvider.InitializeDatabaseAsync();
}

// Eagerly resolve TokenRefreshBroadcaster so it subscribes to ITokenStore.TokensStored
// and forwards refreshed tokens to the UI via IPC.
_ = host.Services.GetRequiredService<TokenRefreshBroadcaster>();

await host.RunAsync();
