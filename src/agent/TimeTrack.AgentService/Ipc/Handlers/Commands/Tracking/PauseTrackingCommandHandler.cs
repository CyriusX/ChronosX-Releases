using System.Security.Cryptography;
using System.Text;
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
/// Handles pausing tracking.
/// Creates a "Tracking Stopped" placeholder session (local only).
/// The session is extended and synced when the user resumes.
/// </summary>
public sealed class PauseTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "PauseTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<PauseTrackingCommandHandler> _logger;

    /// <summary>
    /// Stable hash for the virtual "system://tracking-control" executable path.
    /// Shared across pause/stop/start/resume handlers to identify system sessions.
    /// </summary>
    internal static readonly string SystemExePathHash =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("system://tracking-control")))
            .ToLowerInvariant()[..16];

    public PauseTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        ILogger<PauseTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _sessionRepository = sessionRepository;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            string? reason = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                if (request.Payload.Value.TryGetProperty("reason", out var reasonEl))
                    reason = reasonEl.GetString();
            }

            // Record a "Tracking Stopped" placeholder BEFORE pausing.
            // Saved locally only — the session will be extended to cover the full gap
            // and synced via outbox when the user resumes tracking.
            await RecordTrackingStoppedSessionAsync(reason, ct);

            var result = await _trackingControl.PauseAsync(new PauseTrackingRequest
            {
                Reason = reason ?? "User requested",
                PausedBy = "DesktopHost"
            }, ct);

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    /// <summary>
    /// Creates a 1-second "Tracking Stopped" placeholder session (local only, no outbox).
    /// When the user resumes, StartTracking/ResumeTracking will extend this session
    /// to cover the full gap and sync it to the backend.
    /// </summary>
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
                SystemExePathHash,
                "Tracking Stopped",
                AppCategory.Neutral("system_event", "default"));

            // 1-second placeholder — will be extended on resume
            var period = new TimeRange(now, now.AddSeconds(1));

            var session = ActivitySession.Create(
                userId.Value,
                appIdentity,
                period,
                windowTitle: reason ?? "Monitoramento pausado pelo usuário");

            // Save locally only (no outbox). The final, extended session
            // will be synced when the user resumes tracking.
            await _sessionRepository.SaveAsync(session, ct);

            _logger.LogInformation(
                "Recorded 'Tracking Stopped' placeholder session {SessionId} at {Time}",
                session.Id, now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record 'Tracking Stopped' session");
        }
    }
}
