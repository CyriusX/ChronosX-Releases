using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.AgentService.Events;
using TimeTrack.AgentService.Notifications;
using TimeTrack.AgentService.Workers;
using TimeTrack.MacOSAgentService.Extensions;

// Prevent multiple AgentService instances on macOS (can break tracking logic).
// We use an exclusive lock file because named mutexes are not reliably global across processes on Unix.
System.IO.FileStream? agentLockStream = null;
try
{
    var lockDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimeTrack");
    System.IO.Directory.CreateDirectory(lockDir);
    var lockPath = System.IO.Path.Combine(lockDir, "agent.lock");
    agentLockStream = new System.IO.FileStream(
        lockPath,
        System.IO.FileMode.OpenOrCreate,
        System.IO.FileAccess.ReadWrite,
        System.IO.FileShare.None);
}
catch (Exception ex)
{
    Console.WriteLine($"[MacOSAgentService] Another instance is already running (or lock failed). Exiting. {ex.Message}");
    return;
}

IHost host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((context, services) =>
    {
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

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

try
{
    await host.RunAsync();
}
finally
{
    agentLockStream?.Dispose();
}
