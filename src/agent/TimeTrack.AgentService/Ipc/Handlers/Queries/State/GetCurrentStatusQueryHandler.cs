using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Handles getting current status - returns real tracking state
/// </summary>
public sealed class GetCurrentStatusQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetCurrentStatus";

    private readonly ITrackingStateRepository _trackingStateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetCurrentStatusQueryHandler> _logger;

    public GetCurrentStatusQueryHandler(
        ITrackingStateRepository trackingStateRepository,
        ICurrentUserContext userContext,
        ILogger<GetCurrentStatusQueryHandler> logger)
    {
        _trackingStateRepository = trackingStateRepository;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var userId = _userContext.UserId;

            // Default state when no user is authenticated
            if (userId == null)
            {
                _logger.LogDebug("No authenticated user, returning idle state");
                return SuccessResponse(request.RequestId, new
                {
                    state = "idle",
                    uptime = GetUptimeSeconds(),
                    version = GetVersion()
                });
            }

            var trackingState = await _trackingStateRepository.GetAsync(userId.Value, ct);

            if (trackingState == null)
            {
                _logger.LogDebug("No tracking state found for user {UserId}", userId);
                return SuccessResponse(request.RequestId, new
                {
                    state = "idle",
                    uptime = GetUptimeSeconds(),
                    version = GetVersion()
                });
            }

            var state = MapTrackingStatusToString(trackingState.Status);

            _logger.LogDebug(
                "Current status for user {UserId}: {State} (Status: {TrackingStatus})",
                userId, state, trackingState.Status);

            return SuccessResponse(request.RequestId, new
            {
                state,
                uptime = GetUptimeSeconds(),
                version = GetVersion()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current status");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    private static string MapTrackingStatusToString(TrackingStatus status)
    {
        return status switch
        {
            TrackingStatus.Active => "running",
            TrackingStatus.PausedByUser => "paused",
            TrackingStatus.PausedByPolicy => "paused",
            TrackingStatus.Disabled => "stopped",
            _ => "idle"
        };
    }

    private static int GetUptimeSeconds()
    {
        try
        {
            var startTime = Process.GetCurrentProcess().StartTime;
            return (int)(DateTime.UtcNow - startTime.ToUniversalTime()).TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetVersion()
    {
        try
        {
            return typeof(GetCurrentStatusQueryHandler).Assembly
                .GetName().Version?.ToString(3) ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}
