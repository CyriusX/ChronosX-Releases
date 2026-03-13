namespace TimeTrack.Agent.Domain.Enums;

/// <summary>
/// Tipos de modo de foco disponíveis
/// </summary>
public enum FocusModeType
{
    /// <summary>
    /// Modo desativado
    /// </summary>
    None = 0,

    /// <summary>
    /// Técnica Pomodoro: ciclos curtos de foco com pausas regulares
    /// </summary>
    Pomodoro = 1,

    /// <summary>
    /// Ciclo Ultradian: blocos longos de foco (~90min) com pausas de recuperação
    /// </summary>
    Ultradian = 2
}
