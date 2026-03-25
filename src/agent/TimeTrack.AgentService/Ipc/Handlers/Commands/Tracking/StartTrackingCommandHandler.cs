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
/// Handles starting tracking.
/// When resuming from a pause/stop, extends the "Tracking Stopped" session
/// to cover the full gap, then syncs it to the backend via outbox.
/// </summary>
public sealed class StartTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<StartTrackingCommandHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StartTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        IActivitySessionRepository sessionRepository,
        IOutboxRepository outboxRepository,
        ICurrentUserContext userContext,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        IIpcServer ipcServer,
        ILogger<StartTrackingCommandHandler> logger)
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
            // Don't prevent tracking from starting if extend fails
            _logger.LogWarning(ex, "Failed to extend 'Tracking Stopped' session");
        }
    }
}
