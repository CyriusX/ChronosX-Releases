using TimeTrack.Agent.Domain.Aggregates;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Interface para persistência do estado de tracking
/// </summary>
public interface ITrackingStateRepository
{
    /// <summary>
    /// Obtém o estado de tracking para um usuário específico
    /// </summary>
    Task<TrackingState?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva o estado de tracking
    /// </summary>
    Task SaveAsync(TrackingState state, CancellationToken cancellationToken = default);
}
