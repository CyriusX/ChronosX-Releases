using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Update;

/// <summary>
/// Handles requests to start the update process
/// </summary>
public sealed class StartUpdateCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartUpdate";

    private readonly IUpdateService _updateService;
    private readonly ILogger<StartUpdateCommandHandler> _logger;

    public StartUpdateCommandHandler(
        IUpdateService updateService,
        ILogger<StartUpdateCommandHandler> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Start update requested");

        try
        {
            if (_updateService.IsUpdating)
            {
                _logger.LogWarning("Update already in progress");
                return SuccessResponse(request.RequestId, new
                {
                    started = false,
                    reason = "already_updating",
                    currentProgress = _updateService.CurrentProgress
                });
            }

            // Start the update (fire and forget - progress via events)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _updateService.StartUpdateAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Update task failed");
                }
            }, ct);

            return SuccessResponse(request.RequestId, new
            {
                started = true,
                message = "Update started. Watch for updateProgress events."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting update");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
