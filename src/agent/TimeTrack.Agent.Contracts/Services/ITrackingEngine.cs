using TimeTrack.Agent.Domain.Aggregates;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;

namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Interface principal do engine de tracking
/// </summary>
public interface ITrackingEngine
{
    /// <summary>
    /// Estado atual do tracking
    /// </summary>
    TrackingState State { get; }

    /// <summary>
    /// Inicia o tracking
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pausa o tracking
    /// </summary>
    Task PauseAsync(string reason, string pausedBy, bool isPolicy = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retoma o tracking
    /// </summary>
    Task ResumeAsync(string resumedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evento disparado quando uma sessão é consolidada
    /// </summary>
    event EventHandler<ActivitySession>? OnSessionConsolidated;
}
