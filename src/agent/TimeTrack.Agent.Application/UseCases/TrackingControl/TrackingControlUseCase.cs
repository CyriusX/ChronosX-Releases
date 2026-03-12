using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Aggregates;

namespace TimeTrack.Agent.Application.UseCases.TrackingControl;

/// <summary>
/// Use Case para controle de pausa/retomada do tracking
/// </summary>
public sealed class TrackingControlUseCase
{
    private readonly ITrackingStateRepository _stateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<TrackingControlUseCase> _logger;

    public TrackingControlUseCase(
        ITrackingStateRepository stateRepository,
        ICurrentUserContext userContext,
        ILogger<TrackingControlUseCase> logger)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Pausa o tracking
    /// </summary>
    public async Task<PauseTrackingResponse> PauseAsync(
        PauseTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Reason is required", nameof(request));

        if (string.IsNullOrWhiteSpace(request.PausedBy))
            throw new ArgumentException("PausedBy is required", nameof(request));

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        var state = await GetOrCreateStateAsync(userId, cancellationToken);

        state.Pause(request.Reason, request.PausedBy, request.IsPolicy);

        await _stateRepository.SaveAsync(state, cancellationToken);

        _logger.LogInformation(
            "Tracking paused by {PausedBy}: {Reason} (IsPolicy: {IsPolicy})",
            request.PausedBy, request.Reason, request.IsPolicy);

        return new PauseTrackingResponse
        {
            Status = state.Status.ToString(),
            PausedAt = state.PausedAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Retoma o tracking
    /// </summary>
    public async Task<ResumeTrackingResponse> ResumeAsync(
        ResumeTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResumedBy))
            throw new ArgumentException("ResumedBy is required", nameof(request));

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        var state = await GetOrCreateStateAsync(userId, cancellationToken);

        state.Resume(request.ResumedBy);

        await _stateRepository.SaveAsync(state, cancellationToken);

        _logger.LogInformation(
            "Tracking resumed by {ResumedBy}",
            request.ResumedBy);

        return new ResumeTrackingResponse
        {
            Status = state.Status.ToString(),
            ResumedAt = state.ResumedAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Inicia o tracking (cria novo estado ou reativa existente)
    /// </summary>
    public async Task<StartTrackingResponse> StartAsync(
        StartTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StartedBy))
            throw new ArgumentException("StartedBy is required", nameof(request));

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        var existingState = await _stateRepository.GetAsync(userId, cancellationToken);

        if (existingState == null)
        {
            // No state exists, create new active state
            var newState = TrackingState.CreateActive(userId);
            await _stateRepository.SaveAsync(newState, cancellationToken);

            _logger.LogInformation(
                "Tracking started (new state created) by {StartedBy}",
                request.StartedBy);

            return new StartTrackingResponse
            {
                Status = newState.Status.ToString(),
                StartedAt = newState.UpdatedAt,
                WasCreated = true
            };
        }

        // State exists, handle based on current status
        switch (existingState.Status)
        {
            case Domain.Enums.TrackingStatus.Active:
                // Already active, nothing to do
                _logger.LogInformation("Tracking already active, no action needed");
                return new StartTrackingResponse
                {
                    Status = existingState.Status.ToString(),
                    StartedAt = existingState.UpdatedAt,
                    WasCreated = false
                };

            case Domain.Enums.TrackingStatus.PausedByUser:
            case Domain.Enums.TrackingStatus.PausedByPolicy:
                // Resume from paused
                existingState.Resume(request.StartedBy);
                await _stateRepository.SaveAsync(existingState, cancellationToken);
                _logger.LogInformation("Tracking resumed (was paused) by {StartedBy}", request.StartedBy);
                return new StartTrackingResponse
                {
                    Status = existingState.Status.ToString(),
                    StartedAt = existingState.ResumedAt ?? DateTime.UtcNow,
                    WasCreated = false
                };

            case Domain.Enums.TrackingStatus.Disabled:
                // Enable from disabled
                existingState.Enable();
                await _stateRepository.SaveAsync(existingState, cancellationToken);
                _logger.LogInformation("Tracking enabled (was disabled) by {StartedBy}", request.StartedBy);
                return new StartTrackingResponse
                {
                    Status = existingState.Status.ToString(),
                    StartedAt = existingState.UpdatedAt,
                    WasCreated = false
                };

            default:
                throw new InvalidOperationException($"Unknown tracking status: {existingState.Status}");
        }
    }

    /// <summary>
    /// Para o tracking (desabilita completamente)
    /// </summary>
    public async Task<StopTrackingResponse> StopAsync(
        StopTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoppedBy))
            throw new ArgumentException("StoppedBy is required", nameof(request));

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        var state = await _stateRepository.GetAsync(userId, cancellationToken);

        if (state == null)
        {
            // No state exists, nothing to stop
            _logger.LogInformation("No tracking state to stop");
            return new StopTrackingResponse
            {
                Status = "None",
                StoppedAt = DateTime.UtcNow
            };
        }

        // Disable the tracking state
        state.Disable(request.Reason);
        await _stateRepository.SaveAsync(state, cancellationToken);

        _logger.LogInformation(
            "Tracking stopped by {StoppedBy}: {Reason}",
            request.StoppedBy,
            request.Reason ?? "User requested");

        return new StopTrackingResponse
        {
            Status = state.Status.ToString(),
            StoppedAt = state.UpdatedAt
        };
    }

    /// <summary>
    /// Obtém o estado atual ou cria um novo se não existir
    /// </summary>
    private async Task<TrackingState> GetOrCreateStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var state = await _stateRepository.GetAsync(userId, cancellationToken);

        if (state == null)
        {
            state = TrackingState.CreateActive(userId);
            await _stateRepository.SaveAsync(state, cancellationToken);
            _logger.LogInformation("New tracking state created for user {UserId}", userId);
        }

        return state;
    }
}
