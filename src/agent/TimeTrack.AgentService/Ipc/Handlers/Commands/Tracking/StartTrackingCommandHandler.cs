using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.AgentService.Workers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles starting tracking.
/// When resuming from a pause/stop, extends the "Tracking Stopped" session
/// to cover the full gap, then syncs it to the backend via outbox.
/// </summary>
public sealed class StartTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIpcServer _ipcServer;
    private readonly IAgentEventLogger _eventLogger;
    private readonly IHeartbeatService _heartbeatService;
    private readonly AgentStatusEventBroadcaster _statusBroadcaster;
    private readonly ILogger<StartTrackingCommandHandler> _logger;

    public StartTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        IIpcServer ipcServer,
        IAgentEventLogger eventLogger,
        IHeartbeatService heartbeatService,
        AgentStatusEventBroadcaster statusBroadcaster,
        ILogger<StartTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _sessionRepository = sessionRepository;
        _userContext = userContext;
        _ipcServer = ipcServer;
        _eventLogger = eventLogger;
        _heartbeatService = heartbeatService;
        _statusBroadcaster = statusBroadcaster;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("StartTrackingCommandHandler: HandleAsync started");

        try
        {
            _logger.LogInformation("StartTrackingCommandHandler: IsAuthenticated = {IsAuthenticated}, UserId = {UserId}",
                _userContext.IsAuthenticated, _userContext.UserId);

            if (!_userContext.IsAuthenticated)
            {
                _logger.LogWarning("StartTrackingCommandHandler: User not authenticated, returning error");
                return ErrorResponse(request.RequestId, "User not authenticated");
            }

            // Extend the "Tracking Stopped" session to cover the full gap BEFORE starting,
            // so the gap is filled up to this exact moment.
            await ExtendTrackingStoppedSessionAsync(ct);

            _logger.LogInformation("StartTrackingCommandHandler: Calling TrackingControlUseCase.StartAsync");
            var result = await _trackingControl.StartAsync(new StartTrackingRequest
            {
                StartedBy = "DesktopHost"
            }, ct);

            _logger.LogInformation("StartTrackingCommandHandler: StartAsync completed, Status = {Status}", result.Status);

            // Broadcast state change so DesktopHost tray icon updates
            await _ipcServer.SendEventAsync(new IpcEvent
            {
                EventType = "trackingStateChanged",
                Payload = new { isTracking = true, isPaused = false }
            }, ct);

            await _eventLogger.LogAsync("tracking.started", AgentEventCategory.UserAction, AgentEventSeverity.Info,
                "Monitoramento iniciado pelo usuário", cancellationToken: ct);

            TriggerImmediateHeartbeat();
            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    /// <summary>
    /// Finds the most recent "Tracking Stopped" placeholder session and extends
    /// it to DateTime.UtcNow, then syncs the complete session via outbox.
    /// </summary>
    private async Task ExtendTrackingStoppedSessionAsync(CancellationToken ct)
    {
        try
        {
            var userId = _userContext.UserId;
            if (userId is null) return;

            var mostRecent = await _sessionRepository.GetMostRecentAsync(userId.Value, ct);
            if (mostRecent is null) return;

            // Only extend if it's a "Tracking Stopped" system session
            if (mostRecent.App.ExePathHash != PauseTrackingCommandHandler.SystemExePathHash)
            {
                _logger.LogDebug("Most recent session is not a Tracking Stopped placeholder, skipping extend");
                return;
            }

            var now = DateTime.UtcNow;

            // Extend the session to cover the full gap (pause time → now)
            mostRecent.Extend(now);

            // UpdateAsync saves the new end_utc locally AND creates/updates the outbox
            // item so the extended session syncs to the backend on the next sync cycle.
            await _sessionRepository.UpdateAsync(mostRecent, ct);

            _logger.LogInformation(
                "Extended 'Tracking Stopped' session {SessionId} from {Start} to {End} ({Duration})",
                mostRecent.Id,
                mostRecent.Period.StartUtc,
                mostRecent.Period.EndUtc,
                mostRecent.Duration);
        }
        catch (Exception ex)
        {
            // Don't prevent tracking from starting if extend fails
            _logger.LogWarning(ex, "Failed to extend 'Tracking Stopped' session");
        }
    }

    private void TriggerImmediateHeartbeat()
    {
        try
        {
            var snapshot = new AgentHealthSnapshot(
                HealthStatus: _statusBroadcaster.CurrentHealthStatus,
                BackendReachable: _statusBroadcaster.CurrentBackendReachable,
                ConsecutiveSyncFailures: 0,
                LastSuccessfulSyncAt: null,
                IpcConnected: _ipcServer.IsClientConnected);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = _heartbeatService.SendHeartbeatAsync(snapshot, cts.Token)
                .ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
        }
        catch
        {
            // ignore
        }
    }
}
