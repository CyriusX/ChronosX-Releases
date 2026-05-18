using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.AgentService.Workers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles resuming tracking.
/// Extends the "Tracking Stopped" session to cover the full gap, then syncs it.
/// </summary>
public sealed class ResumeTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ResumeTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIpcServer _ipcServer;
    private readonly IAgentEventLogger _eventLogger;
    private readonly IHeartbeatService _heartbeatService;
    private readonly AgentStatusEventBroadcaster _statusBroadcaster;
    private readonly ILogger<ResumeTrackingCommandHandler> _logger;

    public ResumeTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        IIpcServer ipcServer,
        IAgentEventLogger eventLogger,
        IHeartbeatService heartbeatService,
        AgentStatusEventBroadcaster statusBroadcaster,
        ILogger<ResumeTrackingCommandHandler> logger)
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
        try
        {
            // Extend the "Tracking Stopped" session to cover the full gap
            await ExtendTrackingStoppedSessionAsync(ct);

            var result = await _trackingControl.ResumeAsync(new ResumeTrackingRequest
            {
                ResumedBy = "DesktopHost"
            }, ct);

            // Broadcast state change so DesktopHost tray icon updates
            await _ipcServer.SendEventAsync(new IpcEvent
            {
                EventType = "trackingStateChanged",
                Payload = new { isTracking = true, isPaused = false }
            }, ct);

            await _eventLogger.LogAsync("tracking.resumed", AgentEventCategory.UserAction, AgentEventSeverity.Info,
                "Monitoramento retomado pelo usuário", cancellationToken: ct);

            TriggerImmediateHeartbeat();
            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming tracking");
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
