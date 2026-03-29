using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Handles getting sync errors from the repository
/// </summary>
public sealed class GetErrorsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetErrors";

    private readonly ISyncErrorRepository _syncErrorRepository;
    private readonly ILogger<GetErrorsQueryHandler> _logger;

    public GetErrorsQueryHandler(
        ISyncErrorRepository syncErrorRepository,
        ILogger<GetErrorsQueryHandler> logger)
    {
        _syncErrorRepository = syncErrorRepository;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            // Get errors from the last 24 hours
            var from = DateTime.UtcNow.AddHours(-24);
            var to = DateTime.UtcNow;

            var errors = await _syncErrorRepository.GetByDateRangeAsync(from, to, ct);

            var errorItems = errors.Select(e => new
            {
                id = e.Id.ToString(),
                timestamp = e.TimestampUtc.ToString("O"),
                type = "sync_error",
                message = e.ErrorMessage,
                details = $"Endpoint: {e.Endpoint}, StatusCode: {e.StatusCode}, Attempts: {e.AttemptCount}",
                resolved = false
            }).ToList();

            _logger.LogDebug(
                "Retrieved {Count} errors from the last 24 hours",
                errorItems.Count);

            return SuccessResponse(request.RequestId, new
            {
                errors = errorItems,
                total = errorItems.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting errors");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
