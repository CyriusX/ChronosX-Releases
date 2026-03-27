using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.AgentService.Extensions;

// Configura prioridade do processo o mais cedo possível
var priority = Environment.GetEnvironmentVariable("TIMETRACK_PROCESS_PRIORITY") ?? "BelowNormal";
ServiceCollectionExtensions.ConfigureProcessPriority(priority);

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "ChronosX Agent";
    })
    .ConfigureServices((context, services) =>
    {
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
    })
    .Build();

// Inicializa o banco de dados na inicialização
using (var scope = host.Services.CreateScope())
{
    await scope.ServiceProvider.InitializeDatabaseAsync();
}

await host.RunAsync();
