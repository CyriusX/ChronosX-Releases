using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Aggregates;

namespace TimeTrack.Agent.Application.UseCases.TrackingControl;

/// <summary>
/// Use Case para controle de pausa/retomada do tracking
/// </summary>
public sealed class TrackingControlUseCase
{
    private readonly ITrackingStateRepository _stateRepository;
    private readonly ILogger<TrackingControlUseCase> _logger;

    public TrackingControlUseCase(
        ITrackingStateRepository stateRepository,
        ILogger<TrackingControlUseCase> logger)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
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

        var state = await GetOrCreateStateAsync(cancellationToken);

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

        var state = await GetOrCreateStateAsync(cancellationToken);

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
    /// Obtém o estado atual ou cria um novo se não existir
    /// </summary>
    private async Task<TrackingState> GetOrCreateStateAsync(CancellationToken cancellationToken)
    {
        var state = await _stateRepository.GetAsync(cancellationToken);

        if (state == null)
        {
            state = TrackingState.CreateActive();
            await _stateRepository.SaveAsync(state, cancellationToken);
            _logger.LogInformation("New tracking state created");
        }

        return state;
    }
}
