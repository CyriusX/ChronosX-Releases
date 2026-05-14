using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles stopping tracking.
/// Creates a "Tracking Stopped" placeholder session (local only).
/// The session is extended and synced when the user starts tracking again.
/// </summary>
public sealed class StopTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StopTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIpcServer _ipcServer;
    private readonly IAgentEventLogger _eventLogger;
    private readonly IExceptionReporter _exceptionReporter;
    private readonly ILogger<StopTrackingCommandHandler> _logger;

    public StopTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        IIpcServer ipcServer,
        IAgentEventLogger eventLogger,
        IExceptionReporter exceptionReporter,
        ILogger<StopTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _sessionRepository = sessionRepository;
        _userContext = userContext;
        _ipcServer = ipcServer;
        _eventLogger = eventLogger;
        _exceptionReporter = exceptionReporter;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        string? reason = null;
        if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
        {
            if (request.Payload.Value.TryGetProperty("reason", out var reasonEl))
                reason = reasonEl.GetString();
        }

        // Record a "Tracking Stopped" placeholder BEFORE stopping.
        await RecordTrackingStoppedSessionAsync(reason, ct);

        var result = await _trackingControl.StopAsync(new StopTrackingRequest
        {
            StoppedBy = "DesktopHost",
            Reason = reason
        }, ct);

        // Broadcast state change so DesktopHost tray icon updates
        await _ipcServer.SendEventAsync(new IpcEvent
        {
            EventType = "trackingStateChanged",
            Payload = new { isTracking = false, isPaused = false }
        }, ct);

        await _eventLogger.LogAsync("tracking.stopped", AgentEventCategory.UserAction, AgentEventSeverity.Info,
            "Monitoramento parado pelo usuário", new { reason }, ct);

        return SuccessResponse(request.RequestId, result);
    }

    private async Task RecordTrackingStoppedSessionAsync(string? reason, CancellationToken ct)
    {
        try
        {
            var userId = _userContext.UserId;
            if (userId is null)
            {
                _logger.LogWarning("Cannot record Tracking Stopped session: user not authenticated");
                return;
            }

            var now = DateTime.UtcNow;

            var appIdentity = new AppIdentity(
                PauseTrackingCommandHandler.SystemExePathHash,
                "Tracking Stopped",
                AppCategory.Neutral("system_event", "default"));

            var period = new TimeRange(now, now.AddSeconds(1));

            var session = ActivitySession.Create(
                userId.Value,
                appIdentity,
                period,
                windowTitle: reason ?? "Monitoramento parado pelo usuário");

            // Save locally only — extended and synced on resume/start
            await _sessionRepository.SaveAsync(session, ct);

            _logger.LogInformation(
                "Recorded 'Tracking Stopped' placeholder session {SessionId} at {Time}",
                session.Id, now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record 'Tracking Stopped' session");
            if (!ExceptionReport.IsCancellation(ex))
            {
                var report = ExceptionReport.FromException(
                    ex,
                    component: "AgentService",
                    operation: "ipc.command.StopTracking.recordTrackingStoppedSession");
                await _exceptionReporter.ReportAsync(report, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
