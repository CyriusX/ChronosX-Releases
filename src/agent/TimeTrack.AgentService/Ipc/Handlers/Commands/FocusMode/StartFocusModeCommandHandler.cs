using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles starting focus mode
/// </summary>
public sealed class StartFocusModeCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartFocusMode";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<StartFocusModeCommandHandler> _logger;

    public StartFocusModeCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<StartFocusModeCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var success = _focusModeEngine.Start();
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                started = success,
                state = snapshot.State.ToString(),
                remainingMs = snapshot.RemainingMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting focus mode");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
