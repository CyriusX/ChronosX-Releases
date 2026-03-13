using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Handles getting tracking state
/// </summary>
public sealed class GetTrackingStateQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTrackingState";

    private readonly GetLocalDashboardUseCase _getDashboard;
    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<GetTrackingStateQueryHandler> _logger;

    public GetTrackingStateQueryHandler(
        GetLocalDashboardUseCase getDashboard,
        IFocusModeEngine focusModeEngine,
        ILogger<GetTrackingStateQueryHandler> logger)
    {
        _getDashboard = getDashboard;
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("GetTrackingStateQueryHandler: Starting HandleAsync");
        try
        {
            _logger.LogInformation("GetTrackingStateQueryHandler: Calling _getDashboard.ExecuteAsync");
            var dashboard = await _getDashboard.ExecuteAsync(null, ct);
            _logger.LogInformation("GetTrackingStateQueryHandler: _getDashboard.ExecuteAsync returned");
            var focusSnapshot = _focusModeEngine.GetSnapshot();
            _logger.LogInformation("GetTrackingStateQueryHandler: Focus snapshot retrieved");

            return SuccessResponse(request.RequestId, new
            {
                isTracking = dashboard.TrackingStatus == "Active",
                isPaused = dashboard.TrackingStatus == "Paused",
                isFocusMode = focusSnapshot.State != FocusModeState.Off,
                focusModeState = focusSnapshot.State.ToString(),
                focusModeMode = focusSnapshot.Mode.ToString(),
                focusRemainingMs = focusSnapshot.RemainingMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracking state");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
