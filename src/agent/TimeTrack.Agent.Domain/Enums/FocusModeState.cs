namespace TimeTrack.Agent.Domain.Enums;

/// <summary>
/// Estados possíveis do motor de modo de foco
///
/// Máquina de estados:
/// Off → FocusRunning → BreakRunning → FocusRunning (loop)
///       ↓
///   FocusPaused → FocusRunning
///       ↓
///   Off (via Stop/PolicyChange)
/// </summary>
public enum FocusModeState
{
    /// <summary>
    /// Engine desligado (política desativada ou modo 'none')
    /// </summary>
    Off = 0,

    /// <summary>
    /// Timer de foco ativo, contagem regressiva em andamento
    /// </summary>
    FocusRunning = 1,

    /// <summary>
    /// Usuário pausou manualmente o ciclo
    /// </summary>
    FocusPaused = 2,

    /// <summary>
    /// Timer de pausa ativo (curta ou longa no Pomodoro)
    /// </summary>
    BreakRunning = 3
}
