namespace TimeTrack.Agent.Domain.Enums;

/// <summary>
/// Tipos de pausa no modo Pomodoro
///
/// SRP: Apenas categoriza o tipo de pausa
/// </summary>
public enum BreakType
{
    /// <summary>
    /// Pausa curta entre ciclos (padrão: 5 min)
    /// </summary>
    Short = 0,

    /// <summary>
    /// Pausa longa após N ciclos (padrão: 15 min)
    /// </summary>
    Long = 1
}
