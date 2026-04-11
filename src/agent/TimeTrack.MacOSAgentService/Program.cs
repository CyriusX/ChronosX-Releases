using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.AgentService.Events;
using TimeTrack.AgentService.Notifications;
using TimeTrack.AgentService.Workers;
using TimeTrack.MacOSAgentService.Extensions;

IHost host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((context, services) =>
    {
        services.AddAgentConfiguration(context.Configuration);
        services.AddInfrastructurePersistence(context.Configuration);
        services.AddMacOSInfrastructureProviders();
        services.AddSyncServices(context.Configuration);
        services.AddApplicationLayer(context.Configuration);
        services.AddNotificationAndFocusMode();
        services.AddIpcServer();

        services.AddSingleton<AgentStatusEventBroadcaster>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentStatusEventBroadcaster>());
        services.AddHostedService<TrackingWorker>();
        services.AddHostedService<SyncWorker>();
        services.AddHostedService<MachineMetricsWorker>();
        services.AddHostedService<FocusModeEventBroadcaster>();
        services.AddSingleton<ActivityResumeState>();
        services.AddHostedService<ActivityResumeDetector>();
        services.AddHostedService<TaskIdleWatcher>();

        services.AddUpdateServices(context.Configuration);
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    await scope.ServiceProvider.InitializeDatabaseAsync();
}

await host.RunAsync();
