using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.LocalSettings;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;

/// <summary>
/// Handles updating local settings
/// </summary>
public sealed class UpdateSettingsCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "UpdateSettings";

    private readonly LocalSettingsUseCase _localSettings;
    private readonly ILogger<UpdateSettingsCommandHandler> _logger;

    public UpdateSettingsCommandHandler(
        LocalSettingsUseCase localSettings,
        ILogger<UpdateSettingsCommandHandler> logger)
    {
        _localSettings = localSettings;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            bool? autoResumeNotification = null;
            bool? notificationSounds = null;
            string? language = null;

            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                var payload = request.Payload.Value;

                if (payload.TryGetProperty("autoResumeNotificationEnabled", out var autoResumeProp))
                    autoResumeNotification = autoResumeProp.GetBoolean();

                if (payload.TryGetProperty("notificationSoundsEnabled", out var soundsProp))
                    notificationSounds = soundsProp.GetBoolean();

                if (payload.TryGetProperty("language", out var langProp))
                    language = langProp.GetString();
            }

            var updateRequest = new UpdateLocalSettingsRequest
            {
                AutoResumeNotificationEnabled = autoResumeNotification,
                NotificationSoundsEnabled = notificationSounds,
                Language = language
            };

            var result = await _localSettings.UpdateAsync(updateRequest, ct);

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
