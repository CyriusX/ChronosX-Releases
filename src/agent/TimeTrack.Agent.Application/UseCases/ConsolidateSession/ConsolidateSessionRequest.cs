using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Application.UseCases.ConsolidateSession;

/// <summary>
/// Request para consolidar uma sessão de atividade
/// </summary>
public sealed record ConsolidateSessionRequest
{
    /// <summary>
    /// Sessão de atividade a ser consolidada
    /// </summary>
    public required ActivitySession Session { get; init; }
}

/// <summary>
/// Response da consolidação de sessão
/// </summary>
public sealed record ConsolidateSessionResponse
{
    /// <summary>
    /// ID da sessão consolidada
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Indica se a sessão foi mesclada com uma anterior
    /// </summary>
    public bool WasMerged { get; init; }

    /// <summary>
    /// Duração final da sessão
    /// </summary>
    public TimeSpan Duration { get; init; }
}
