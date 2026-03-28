using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.AgentService.Ipc.Handlers;

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
    private readonly IOutboxRepository _outboxRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<ResumeTrackingCommandHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ResumeTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        IOutboxRepository outboxRepository,
        ICurrentUserContext userContext,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        IIpcServer ipcServer,
        ILogger<ResumeTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _sessionRepository = sessionRepository;
        _outboxRepository = outboxRepository;
        _userContext = userContext;
        _idempotencyKeyGenerator = idempotencyKeyGenerator;
        _ipcServer = ipcServer;
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

            // Update the session locally
            await _sessionRepository.UpdateAsync(mostRecent, ct);

            // Create outbox item with the final duration so it syncs to the backend
            var payloadJson = JsonSerializer.Serialize(new
            {
                id = mostRecent.Id,
                exePathHash = mostRecent.App.ExePathHash,
                displayName = mostRecent.App.DisplayName,
                categoryProductivity = mostRecent.App.Category.Productivity,
                categorySubcategory = mostRecent.App.Category.Subcategory,
                categorySource = mostRecent.App.Category.Source,
                startUtc = mostRecent.Period.StartUtc,
                endUtc = mostRecent.Period.EndUtc,
                windowHash = mostRecent.WindowHash,
                windowTitle = mostRecent.WindowTitle,
                filePath = mostRecent.FilePath
            }, JsonOptions);

            var idempotencyKey = _idempotencyKeyGenerator.Generate(
                "activity_session", mostRecent.Id, mostRecent.Period.StartUtc);

            var outboxItem = OutboxItem.Create(
                "activity_session", mostRecent.Id, payloadJson, idempotencyKey);

            await _outboxRepository.AddAsync(outboxItem, ct);

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
}
