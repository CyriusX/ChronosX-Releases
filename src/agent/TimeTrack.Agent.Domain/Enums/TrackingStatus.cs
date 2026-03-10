namespace TimeTrack.Agent.Domain.Enums;

/// <summary>
/// Representa os possíveis estados do tracking
/// </summary>
public enum TrackingStatus
{
    /// <summary>
    /// Coleta em andamento
    /// </summary>
    Active = 1,

    /// <summary>
    /// Pausado manualmente pelo colaborador
    /// </summary>
    PausedByUser = 2,

    /// <summary>
    /// Pausado por regra de política da organização
    /// </summary>
    PausedByPolicy = 3,

    /// <summary>
    /// Desativado pelo administrador
    /// </summary>
    Disabled = 4
}
