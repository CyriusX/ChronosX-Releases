using Microsoft.Extensions.DependencyInjection;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Auth;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;
using TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Sync;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;
using TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;
using TimeTrack.AgentService.Ipc.Handlers.Queries.Data;
using TimeTrack.AgentService.Ipc.Handlers.Queries.State;

namespace TimeTrack.AgentService.Ipc.Handlers;

/// <summary>
/// Extension methods for registering IPC handlers
/// </summary>
public static class IpcHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all IPC command and query handlers
    /// </summary>
    public static IServiceCollection AddIpcHandlers(this IServiceCollection services)
    {
        // Command Handlers
        services.AddSingleton<IIpcCommandHandler, StoreTokensCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, StartTrackingCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, StopTrackingCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, PauseTrackingCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, ResumeTrackingCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, StartFocusModeCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, StopFocusModeCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, PauseFocusModeCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, ResumeFocusModeCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, SkipBreakCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, ApplyFocusPolicyCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, SyncNowCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, ResetOutboxCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, AssignProjectCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, AssignTaskCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, UpdateSettingsCommandHandler>();
        services.AddSingleton<IIpcCommandHandler, SetWorkHoursCommandHandler>();

        // Query Handlers
        services.AddSingleton<IIpcQueryHandler, GetCurrentSessionQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetTodaySummaryQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetRecentActivitiesQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetRecentAppsQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetTrackingStateQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetCurrentStatusQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetSyncStateQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetFocusModeStateQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetProjectsQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetTasksQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetErrorsQueryHandler>();
        services.AddSingleton<IIpcQueryHandler, GetSettingsQueryHandler>();

        // Router
        services.AddSingleton<IpcMessageRouter>();

        return services;
    }
}
