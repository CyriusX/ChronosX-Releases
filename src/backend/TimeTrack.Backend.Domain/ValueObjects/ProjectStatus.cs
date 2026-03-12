namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Status do projeto
/// </summary>
public enum ProjectStatus
{
    /// <summary>
    /// Projeto ativo - pode receber entradas de tempo
    /// </summary>
    Active = 1,

    /// <summary>
    /// Projeto arquivado - apenas leitura
    /// </summary>
    Archived = 2
}
