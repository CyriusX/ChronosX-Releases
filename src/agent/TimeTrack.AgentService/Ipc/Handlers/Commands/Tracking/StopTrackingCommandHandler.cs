using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles stopping tracking
/// </summary>
public sealed class StopTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StopTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly ILogger<StopTrackingCommandHandler> _logger;

    public StopTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        ILogger<StopTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            string? reason = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                reason = request.Payload.Value.GetProperty("reason").GetString();
            }

            var result = await _trackingControl.StopAsync(new StopTrackingRequest
            {
                StoppedBy = "DesktopHost",
                Reason = reason
            }, ct);

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
