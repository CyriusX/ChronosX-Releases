using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles pausing focus mode
/// </summary>
public sealed class PauseFocusModeCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "PauseFocusMode";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<PauseFocusModeCommandHandler> _logger;

    public PauseFocusModeCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<PauseFocusModeCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var success = _focusModeEngine.Pause();
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                paused = success,
                state = snapshot.State.ToString(),
                remainingMs = snapshot.RemainingMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing focus mode");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
