using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Update;

/// <summary>
/// Handles requests to get current update progress
/// </summary>
public sealed class GetUpdateProgressQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetUpdateProgress";

    private readonly IUpdateService _updateService;
    private readonly ILogger<GetUpdateProgressQueryHandler> _logger;

    public GetUpdateProgressQueryHandler(
        IUpdateService updateService,
        ILogger<GetUpdateProgressQueryHandler> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var progress = _updateService.CurrentProgress;
            var lastCheck = _updateService.LastCheckResult;

            return Task.FromResult(SuccessResponse(request.RequestId, new
            {
                isUpdating = _updateService.IsUpdating,
                stage = progress?.Stage.ToString() ?? "idle",
                percentage = progress?.Percentage ?? 0,
                message = progress?.Message ?? string.Empty,
                bytesDownloaded = progress?.BytesDownloaded,
                bytesTotal = progress?.BytesTotal,
                bytesPerSecond = progress?.BytesPerSecond,
                targetVersion = progress?.TargetVersion,
                errorMessage = progress?.ErrorMessage,
                lastCheckVersion = lastCheck?.LatestVersion,
                lastCheckHasUpdate = lastCheck?.HasUpdate
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting update progress");
            return Task.FromResult(UnknownErrorResponse(request.RequestId, ex));
        }
    }
}
