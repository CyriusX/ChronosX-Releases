namespace TimeTrack.Agent.Domain.Events;

/// <summary>
/// Interface base para domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Momento em que o evento ocorreu
    /// </summary>
    DateTime OccurredAt { get; }
}
