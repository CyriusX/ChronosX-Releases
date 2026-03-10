using TimeTrack.Agent.Domain.Aggregates;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Interface para persistência do estado de tracking
/// </summary>
public interface ITrackingStateRepository
{
    /// <summary>
    /// Obtém o estado atual de tracking
    /// </summary>
    Task<TrackingState?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva o estado de tracking
    /// </summary>
    Task SaveAsync(TrackingState state, CancellationToken cancellationToken = default);
}
