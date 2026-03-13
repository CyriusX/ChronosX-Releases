using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Application.UseCases.ConsolidateSession;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.Agent.Application.UseCases.GetSyncState;
using TimeTrack.Agent.Application.UseCases.LocalSettings;
using TimeTrack.Agent.Application.UseCases.RecordActiveWindow;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Application.Extensions;

/// <summary>
/// Extension methods para configurar serviços do Application layer
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona todos os Use Cases do Application layer
    /// </summary>
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        // Use Cases
        services.AddSingleton<ConsolidateSessionUseCase>();
        services.AddSingleton<TrackingControlUseCase>();
        services.AddSingleton<GetLocalDashboardUseCase>();
        services.AddSingleton<GetSyncStateUseCase>();
        services.AddSingleton<RecordActiveWindowUseCase>();
        services.AddSingleton<LocalSettingsUseCase>();

        return services;
    }

    /// <summary>
    /// Adiciona serviços do modo de foco
    /// Note: INotificationService deve ser registrado antes de chamar este método
    /// </summary>
    public static IServiceCollection AddFocusModeServices(this IServiceCollection services)
    {
        services.AddSingleton<IFocusModeEngine, FocusModeEngine>();

        return services;
    }
}
