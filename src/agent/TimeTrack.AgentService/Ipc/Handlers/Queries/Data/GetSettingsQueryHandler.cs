using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.LocalSettings;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Handles getting local settings
/// </summary>
public sealed class GetSettingsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetSettings";

    private readonly LocalSettingsUseCase _localSettings;
    private readonly ILogger<GetSettingsQueryHandler> _logger;

    public GetSettingsQueryHandler(
        LocalSettingsUseCase localSettings,
        ILogger<GetSettingsQueryHandler> logger)
    {
        _localSettings = localSettings;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var settings = await _localSettings.GetAsync(ct);

            return SuccessResponse(request.RequestId, new
            {
                autoResumeNotificationEnabled = settings.AutoResumeNotificationEnabled,
                notificationSoundsEnabled = settings.NotificationSoundsEnabled,
                language = settings.Language,
                idleThresholdSeconds = settings.IdleThresholdSeconds,
                workGoalSeconds = settings.WorkGoalSeconds,
                updatedAt = settings.UpdatedAt.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
