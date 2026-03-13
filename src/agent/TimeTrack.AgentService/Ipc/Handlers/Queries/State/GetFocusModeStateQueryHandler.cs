using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Handles getting focus mode state
/// </summary>
public sealed class GetFocusModeStateQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetFocusModeState";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<GetFocusModeStateQueryHandler> _logger;

    public GetFocusModeStateQueryHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<GetFocusModeStateQueryHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                state = snapshot.State.ToString(),
                mode = snapshot.Mode.ToString(),
                remainingMs = snapshot.RemainingMs,
                cycleNumber = snapshot.CycleNumber,
                totalCyclesToday = snapshot.TotalCyclesToday,
                nextBreakType = snapshot.NextBreakType.ToString(),
                cycleStartedAt = snapshot.CycleStartedAt?.ToString("O"),
                plannedDurationMs = snapshot.PlannedDurationMs,
                allowUserOverride = snapshot.AllowUserOverride,
                timestamp = snapshot.Timestamp.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting focus mode state");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
